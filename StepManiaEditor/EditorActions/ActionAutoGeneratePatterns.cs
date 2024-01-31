﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;
using static StepManiaLibrary.ExpressedChart.ExpressedChart;
using Config = StepManiaLibrary.ExpressedChart.Config;

namespace StepManiaEditor;

/// <summary>
/// Action to autogenerate steps for one or more EditorPatternEvents.
/// </summary>
internal sealed class ActionAutoGeneratePatterns : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorChart EditorChart;
	private readonly List<EditorPatternEvent> Patterns;
	private readonly bool UseNewSeeds;
	private readonly List<EditorEvent> DeletedEvents = new();
	private readonly List<EditorEvent> AddedEvents = new();

	public ActionAutoGeneratePatterns(
		Editor editor,
		EditorChart editorChart,
		IEnumerable<EditorPatternEvent> allPatterns,
		bool useNewSeeds) : base(true, false)
	{
		Editor = editor;
		EditorChart = editorChart;
		Patterns = new List<EditorPatternEvent>();
		Patterns.AddRange(allPatterns);
		UseNewSeeds = useNewSeeds;
	}

	public override string ToString()
	{
		if (Patterns.Count == 1)
			return $"Autogenerate \"{Patterns[0].GetPatternConfig()}\" Pattern at row {Patterns[0].ChartRow}.";
		return $"Autogenerate {Patterns.Count} Patterns.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void UndoImplementation()
	{
		// To undo this action synchronously delete the newly added events and re-add the deleted events.
		EditorChart.DeleteEvents(AddedEvents);
		EditorChart.AddEvents(DeletedEvents);
	}

	protected override void DoImplementation()
	{
		// Check for redo and avoid doing the work again.
		if (AddedEvents.Count > 0 || DeletedEvents.Count > 0)
		{
			EditorChart.DeleteEvents(DeletedEvents);
			EditorChart.AddEvents(AddedEvents);
			OnDone();
			return;
		}

		if (Patterns.Count == 0)
		{
			OnDone();
			return;
		}

		var errorString = Patterns.Count == 1 ? "Failed to generate pattern." : "Failed to generate patterns.";

		// Get the StepGraph.
		if (!Editor.GetStepGraph(EditorChart.ChartType, out var stepGraph) || stepGraph == null)
		{
			Logger.Error($"{errorString} No {ImGuiUtils.GetPrettyEnumString(EditorChart.ChartType)} StepGraph is loaded.");
			OnDone();
			return;
		}

		// Get the ExpressedChart Config.
		var expressedChartConfig = ExpressedChartConfigManager.Instance.GetConfig(EditorChart.ExpressedChartConfig);
		if (expressedChartConfig == null)
		{
			Logger.Error($"{errorString} No {EditorChart.ExpressedChartConfig} Expressed Chart Config defined.");
			OnDone();
			return;
		}

		// Delete all events which overlap regions to fill based on the patterns.
		DeletedEvents.AddRange(ActionDeletePatternNotes.DeleteEventsOverlappingPatterns(EditorChart, Patterns));

		// Asynchronously generate the patterns.
		DoPatternGenerationAsync(stepGraph, expressedChartConfig.Config);
	}

	/// <summary>
	/// Performs the bulk of the event generation logic.
	/// This logic is run asynchronously and when it is complete the generated EditorEvents
	/// are added back to the EditorChart synchronously.
	/// </summary>
	/// <param name="stepGraph">The StepGraph for the EditorChart.</param>
	/// <param name="expressedChartConfig">The ExpressedChart Config for the EditorChart.</param>
	private async void DoPatternGenerationAsync(StepGraph stepGraph, Config expressedChartConfig)
	{
		// Generate patterns asynchronously.
		await Task.Run(() =>
		{
			try
			{
				GeneratePatterns(stepGraph, expressedChartConfig);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to generate patterns. {e}");
			}
		});

		// Async work is done, add the newly generated EditorEvents.
		EditorChart.AddEvents(AddedEvents);
		OnDone();
	}

	/// <summary>
	/// Generates all EditorEvents for all patterns.
	/// Does not modify the EditorChart.
	/// Accumulates new EditorEvents in AddedEvents.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="expressedChartConfig">ExpressedChart Config for the chart.</param>
	private void GeneratePatterns(StepGraph stepGraph, Config expressedChartConfig)
	{
		// Get the timing events. These are needed by the PerformedChart to properly time new events to support
		// generation logic which relies on time.
		var timingEvents = EditorChart.GetSmTimingEvents();

		// Get the NPS. This is needed by the PerformedChart to properly account for relative density.
		var nps = GetNps();

		// Create Events from the EditorChart
		var chartEvents = EditorChart.GenerateSmEvents();

		// TODO: Potential optimization: Don't get more events than needed to generate the fist pattern.
		// We should cap this to safely determine following footing, without going to the end of the chart.
		var editorEvents = new EventTree(EditorChart);
		foreach (var editorEvent in EditorChart.GetEvents())
		{
			editorEvents.Insert(editorEvent);
		}

		// Generate EditorEvents for each pattern in order.
		for (var patternIndex = 0; patternIndex < Patterns.Count; patternIndex++)
		{
			var pattern = Patterns[patternIndex];
			var errorString = $"Failed to generate {pattern.GetMiscEventText()} pattern at row {pattern.GetRow()}.";

			if (pattern.GetNumSteps() <= 0)
			{
				Logger.Warn($"{errorString} Pattern range is too short to generate steps.");
				continue;
			}

			var transitionCutoffPercentage = pattern.GetPerformedChartConfig().Config.Transitions.TransitionCutoffPercentage;
			var numStepsAtLastTransition = -1;
			var totalNodeSteps = 0;
			bool? lastTransitionLeft = null;
			var nextPattern = patternIndex < Patterns.Count - 1 ? Patterns[patternIndex + 1] : null;

			// Create an ExpressedChart.
			// TODO: Potential optimization: Only consider the surrounding notes from the pattern.
			// This will involve also getting the previous rate altering event so we can include the
			// correct tempo, etc.
			var expressedChart = CreateFromSMEvents(
				chartEvents,
				stepGraph,
				expressedChartConfig,
				EditorChart.Rating);
			if (expressedChart == null)
			{
				Logger.Error($"{errorString} Could not create Expressed Chart.");
				continue;
			}

			// Get the surrounding step information and counts per lane so we can provide them to the PerformedChart
			// pattern generation logic.
			var previousStepFoot = Constants.InvalidFoot;
			var previousStepTime = new double[Constants.NumFeet];
			var previousFooting = new int[Constants.NumFeet];
			var followingStepFoot = Constants.InvalidFoot;
			var followingFooting = new int[Constants.NumFeet];
			for (var i = 0; i < Constants.NumFeet; i++)
			{
				previousFooting[i] = Constants.InvalidFoot;
				followingFooting[i] = Constants.InvalidFoot;
			}

			var currentLaneCounts = new int[stepGraph.NumArrows];
			var firstStepRow = pattern.GetFirstStepRow();

			// Loop over all ExpressedChart search nodes.
			// The nodes give us GraphNodes, which let us determine which arrows are associated with which feet.
			var currentExpressedChartSearchNode = expressedChart.GetRootSearchNode();
			ChartSearchNode previousExpressedChartSearchNode = null;
			var foundPreviousFooting = false;
			// TODO: Potential optimization: Only consider the surrounding notes from the pattern.
			var editorEventEnumerator = editorEvents.First();
			editorEventEnumerator.MoveNext();
			while (currentExpressedChartSearchNode != null)
			{
				// This search node follows the pattern.
				// Check for updating following footing.
				if (currentExpressedChartSearchNode.Position >= firstStepRow)
				{
					foundPreviousFooting = true;

					// Now that we have passed into the range of the pattern, back up to check the preceding notes.
					GetPrecedingFooting(
						stepGraph,
						previousExpressedChartSearchNode,
						editorEventEnumerator.Clone(),
						out previousStepTime,
						out previousStepFoot,
						out previousFooting);

					// Scan forward to get the following footing.
					GetFollowingFooting(
						stepGraph,
						currentExpressedChartSearchNode,
						editorEventEnumerator.Clone(),
						out followingStepFoot,
						out followingFooting);

					// Stop the search.
					break;
				}

				// Track transition information.
				var isRelease = currentExpressedChartSearchNode.PreviousLink?.GraphLink?.IsRelease() ?? false;
				if (!isRelease)
					totalNodeSteps++;
				stepGraph.GetSide(currentExpressedChartSearchNode.GraphNode, transitionCutoffPercentage, out var leftSide);
				if (leftSide != null && lastTransitionLeft != leftSide)
				{
					if (lastTransitionLeft != null)
					{
						numStepsAtLastTransition = totalNodeSteps;
					}

					lastTransitionLeft = leftSide;
				}

				// Advance the enumerator for editorEvents and accumulate steps per lane.
				while (editorEventEnumerator.IsCurrentValid()
				       && editorEventEnumerator.Current!.GetRow() <= currentExpressedChartSearchNode.Position)
				{
					if (!pattern.IgnorePrecedingDistribution)
					{
						if (editorEventEnumerator.Current is EditorTapNoteEvent or EditorHoldNoteEvent or EditorFakeNoteEvent
						    or EditorLiftNoteEvent)
						{
							currentLaneCounts[editorEventEnumerator.Current.GetLane()]++;
						}
					}

					editorEventEnumerator.MoveNext();
				}

				previousExpressedChartSearchNode = currentExpressedChartSearchNode;
				currentExpressedChartSearchNode = currentExpressedChartSearchNode.GetNextNode();
			}

			// In the case where no notes follow the pattern, check for finding the preceding footing.
			if (!foundPreviousFooting)
			{
				if (previousExpressedChartSearchNode != null)
				{
					GetPrecedingFooting(
						stepGraph,
						previousExpressedChartSearchNode,
						editorEventEnumerator.Clone(),
						out previousStepTime,
						out previousStepFoot,
						out previousFooting);
				}
			}

			// If there are no previous notes, use the default position.
			if (previousFooting[Constants.L] == Constants.InvalidArrowIndex)
				previousFooting[Constants.L] = stepGraph.GetRoot().State[Constants.L, Constants.DefaultFootPortion].Arrow;
			if (previousFooting[Constants.R] == Constants.InvalidArrowIndex)
				previousFooting[Constants.R] = stepGraph.GetRoot().State[Constants.R, Constants.DefaultFootPortion].Arrow;

			// Due to the above logic to assign footing to the default state it is possible
			// for both feet to be assigned to the same arrow. Correct that.
			if (previousFooting[Constants.L] == previousFooting[Constants.R])
			{
				previousFooting[Constants.L] = stepGraph.GetRoot().State[Constants.L, Constants.DefaultFootPortion].Arrow;
				previousFooting[Constants.R] = stepGraph.GetRoot().State[Constants.R, Constants.DefaultFootPortion].Arrow;
			}

			// If we don't know what foot to start on, choose a starting foot.
			if (previousStepFoot == Constants.InvalidFoot)
			{
				// If we know the following foot, choose a starting foot that will lead into it
				// through alternating.
				if (followingStepFoot != Constants.InvalidArrowIndex)
				{
					var numStepsInPattern = pattern.GetNumSteps();

					// Even number of steps, start on the same foot.
					if (numStepsInPattern % 2 == 0)
					{
						previousStepFoot = Constants.OtherFoot(followingStepFoot);
					}
					// Otherwise, start on the opposite foot.
					else
					{
						previousStepFoot = followingStepFoot;
					}
				}
				// Otherwise, start on the right foot.
				else
				{
					previousStepFoot = Constants.L;
				}
			}

			// Create a PerformedChart section for the Pattern.
			var performedChart = PerformedChart.CreateWithPattern(stepGraph,
				pattern.GetPatternConfig().Config,
				pattern.GetPerformedChartConfig().Config,
				pattern.GetFirstStepRow(),
				pattern.GetLastStepRow(),
				UseNewSeeds ? new Random().Next() : pattern.RandomSeed,
				previousStepFoot,
				previousStepTime,
				previousFooting,
				followingFooting,
				currentLaneCounts,
				timingEvents,
				totalNodeSteps,
				numStepsAtLastTransition,
				lastTransitionLeft,
				nps,
				pattern.GetMiscEventText());
			if (performedChart == null)
			{
				Logger.Error($"{errorString} Could not create Performed Chart.");
				continue;
			}

			// Convert this PerformedChart section to Stepmania Events.
			var smEvents = performedChart.CreateSMChartEvents();
			var smEventsToAdd = smEvents;

			// Check for excluding some Events. It is possible that future patterns will
			// overlap this pattern. In that case we do not want to add the notes from
			// this pattern which overlap, and we instead want to let the next pattern
			// generate those notes.
			if (nextPattern != null && nextPattern.GetNumSteps() > 0)
			{
				var nextPatternStartRow = nextPattern.GetFirstStepRow();
				if (nextPatternStartRow <= pattern.GetEndRow())
				{
					smEventsToAdd = new List<Event>();
					foreach (var smEvent in smEvents)
					{
						if (smEvent.IntegerPosition >= nextPatternStartRow)
							break;
						smEventsToAdd.Add(smEvent);
					}
				}
			}

			// Update the running list of all Events.
			chartEvents.AddRange(smEventsToAdd);
			chartEvents.Sort(new SMEventComparer());
			// TODO: Potential Optimization: only update times on smEventsToAdd.
			// Note that this is technically mutating existing Events that are unrelated to the patterns.
			// We shouldn't be doing this, but it should also have no effect because if it did that would mean
			// existing event timing was wrong.
			SetEventTimeAndMetricPositionsFromRows(chartEvents);

			// Convert new events to EditorEvents.
			var newEditorEvents = new List<EditorEvent>();
			foreach (var smEvent in smEventsToAdd)
			{
				var newEditorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(EditorChart, smEvent));
				newEditorEvents.Add(newEditorEvent);
				editorEvents.Insert(newEditorEvent);
			}

			// Update the running list of all added EditorEvents.
			AddedEvents.AddRange(newEditorEvents);
		}
	}

	/// <summary>
	/// Helper function to get the preceding footing of a pattern.
	/// If preceding steps are brackets only the DefaultFootPortion (Heel)'s lane will be used.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="node">The ChartSearchNode of the last event preceding the pattern.</param>
	/// <param name="editorEventEnumerator">
	/// The EditorEvent enumerator of the last EditorEvent preceding the pattern.
	/// </param>
	/// <param name="previousStepTime">
	/// Out parameter to record the time of the most recent preceding step.
	/// </param>
	/// <param name="previousStepFoot">
	/// Out parameter to record the foot used to step on the most recent preceding step.
	/// </param>
	/// <param name="previousFooting">
	/// Out parameter to record the lane stepped on per foot of the preceding steps.
	/// </param>
	private static void GetPrecedingFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator editorEventEnumerator,
		out double[] previousStepTime,
		out int previousStepFoot,
		out int[] previousFooting)
	{
		// Initialize out parameters.
		previousStepFoot = Constants.InvalidFoot;
		previousStepTime = new double[Constants.NumFeet];
		previousFooting = new int[Constants.NumFeet];
		for (var i = 0; i < Constants.NumFeet; i++)
		{
			previousFooting[i] = Constants.InvalidFoot;
		}

		// Scan backwards.
		var numFeetFound = 0;
		var positionOfCurrentSteps = -1;
		var currentSteppedLanes = new bool[stepGraph.NumArrows];
		while (node != null)
		{
			// If we have scanned backwards into a new row, update the currently stepped on lanes for that row.
			CheckAndUpdateCurrentSteppedLanes(stepGraph, node, editorEventEnumerator, ref positionOfCurrentSteps,
				ref currentSteppedLanes, false);

			// Update the tracked footing based on the currently stepped on lanes.
			CheckAndUpdateFooting(stepGraph, node, previousFooting, currentSteppedLanes, ref numFeetFound, ref previousStepFoot,
				ref previousStepTime);

			if (numFeetFound == Constants.NumFeet)
				break;

			// Advance.
			node = node.PreviousNode;
		}
	}

	/// <summary>
	/// Helper function to get the following footing of a pattern.
	/// If following steps are brackets only the DefaultFootPortion (Heel)'s lane will be used.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="node">The ChartSearchNode of the first event following the pattern.</param>
	/// <param name="editorEventEnumerator">
	/// The EditorEvent enumerator of the first EditorEvent following the pattern.
	/// </param>
	/// <param name="followingStepFoot">
	/// Out parameter to record the next foot which steps first in the following steps.
	/// </param>
	/// <param name="followingFooting">
	/// Out parameter to record the lane stepped on per foot of the following steps.
	/// </param>
	private static void GetFollowingFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator editorEventEnumerator,
		out int followingStepFoot,
		out int[] followingFooting)
	{
		// Initialize out parameters.
		followingFooting = new int[Constants.NumFeet];
		for (var i = 0; i < Constants.NumFeet; i++)
		{
			followingFooting[i] = Constants.InvalidFoot;
		}

		// Unused variables, but they simplify the common footing update logic.
		followingStepFoot = Constants.InvalidFoot;
		var followingStepTime = new double[Constants.NumFeet];

		// The enumerator is already beyond the pattern. We want to back up one to easily examine
		// the steps following the pattern.

		// If the enumerator has moved beyond the final note, back it up one.
		if (!editorEventEnumerator.IsCurrentValid())
			editorEventEnumerator.MovePrev();

		// Back up until we precede the row following the pattern.
		while (editorEventEnumerator.IsCurrentValid() && editorEventEnumerator.Current!.GetRow() >= node.Position)
		{
			if (!editorEventEnumerator.MovePrev())
			{
				editorEventEnumerator.MoveNext();
				break;
			}
		}

		// Scan forwards.
		var numFeetFound = 0;
		var positionOfCurrentSteps = -1;
		var currentSteppedLanes = new bool[stepGraph.NumArrows];
		while (node != null)
		{
			// If we have scanned forward into a new row, update the currently stepped on lanes for that row.
			CheckAndUpdateCurrentSteppedLanes(stepGraph, node, editorEventEnumerator, ref positionOfCurrentSteps,
				ref currentSteppedLanes, true);

			// Update the tracked footing based on the currently stepped on lanes.
			CheckAndUpdateFooting(stepGraph, node, followingFooting, currentSteppedLanes, ref numFeetFound, ref followingStepFoot,
				ref followingStepTime);

			if (numFeetFound == Constants.NumFeet)
				break;

			// Advance.
			node = node.GetNextNode();
		}
	}

	/// <summary>
	/// Helper function for updating an array of currently stepped on lanes when scanning and the row changes.
	/// The currently stepped on lanes are used for determining footing when comparing against a GraphNode.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the chart.</param>
	/// <param name="node">
	/// Current ChartSearchNode. If the position of the current steps doesn't equal this node's position
	/// then the currentSteppedLanes will be updated accordingly.
	/// </param>
	/// <param name="editorEventEnumerator">
	/// Enumerator of the EditorEvents to use for scanning to determine which lanes are stepped on.
	/// </param>
	/// <param name="positionOfCurrentSteps">
	/// Last position of the currentSteppedLanes. Will be updated if currentSteppedLanes are updated.
	/// </param>
	/// <param name="currentSteppedLanes">
	/// Array of bools, one per lane. This will be updated to reflect which lanes have steps on them
	/// if the positionOfCurrentSteps is old and needs to be updated based on the given node's position.
	/// </param>
	/// <param name="scanForward">
	/// If true, scan forward for following steps. If false, scan backwards for preceding steps.
	/// </param>
	private static void CheckAndUpdateCurrentSteppedLanes(
		StepGraph stepGraph,
		ChartSearchNode node,
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator editorEventEnumerator,
		ref int positionOfCurrentSteps,
		ref bool[] currentSteppedLanes,
		bool scanForward)
	{
		// Determine the steps which occur at the row of this node, so we can assign feet to them.
		if (positionOfCurrentSteps != node.Position)
		{
			// Clear stepped lanes.
			for (var i = 0; i < stepGraph.NumArrows; i++)
				currentSteppedLanes[i] = false;

			// Scan the current row, recording the lanes being stepped on at this position.
			while (editorEventEnumerator.IsCurrentValid() &&
			       (scanForward
				       ? editorEventEnumerator.Current!.GetRow() <= node.Position
				       : editorEventEnumerator.Current!.GetRow() >= node.Position))
			{
				if (editorEventEnumerator.Current.GetRow() == node.Position)
				{
					if (editorEventEnumerator.Current is EditorTapNoteEvent or EditorHoldNoteEvent or EditorFakeNoteEvent
					    or EditorLiftNoteEvent)
					{
						currentSteppedLanes[editorEventEnumerator.Current.GetLane()] = true;
					}
				}

				if (scanForward)
					editorEventEnumerator.MoveNext();
				else
					editorEventEnumerator.MovePrev();
			}

			// Update the position we have recorded steps for.
			positionOfCurrentSteps = node.Position;
		}
	}

	/// <summary>
	/// Helper function to update preceding or following footing.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the chart.</param>
	/// <param name="node">Current ChartSearchNode.</param>
	/// <param name="footing">
	/// Array of lanes per foot representing previous or following footing to fill.
	/// Will be updated as footing is found.
	/// </param>
	/// <param name="steppedLanes">
	/// Array of bools per lane representing which lanes are currently stepped on.
	/// </param>
	/// <param name="numFeetFound">
	/// Number of feet whose footing is currently found. Will be updated as footing
	/// is found.
	/// </param>
	/// <param name="stepFoot">
	/// Foot of the first preceding or following step to set.
	/// </param>
	/// <param name="stepFootTime">
	/// Array of time per foot representing the times of the previous or following steps.
	/// Time of the first preceding of following step to set.
	/// </param>
	private static void CheckAndUpdateFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		int[] footing,
		bool[] steppedLanes,
		ref int numFeetFound,
		ref int stepFoot,
		ref double[] stepFootTime)
	{
		// With the stepped on lanes known, use the GraphNodes to determine which foot stepped
		// on each lane.
		if (node.PreviousLink != null && !node.PreviousLink.GraphLink.IsRelease())
		{
			for (var f = 0; f < Constants.NumFeet; f++)
			{
				if (footing[f] != Constants.InvalidFoot)
					continue;
				for (var p = 0; p < Constants.NumFootPortions; p++)
				{
					if (footing[f] != Constants.InvalidFoot)
						continue;

					if (node.GraphNode.State[f, p].State != GraphArrowState.Lifted)
					{
						for (var a = 0; a < stepGraph.NumArrows; a++)
						{
							if (steppedLanes[a] && a == node.GraphNode.State[f, p].Arrow)
							{
								if (stepFoot == Constants.InvalidFoot)
									stepFoot = f;

								footing[f] = node.GraphNode.State[f, p].Arrow;
								stepFootTime[f] = node.TimeSeconds;
								numFeetFound++;
								break;
							}
						}
					}

					if (numFeetFound == Constants.NumFeet)
						break;
				}

				if (numFeetFound == Constants.NumFeet)
					break;
			}
		}
	}

	/// <summary>
	/// Gets the notes per second of the chart including the notes from the patterns for this action.
	/// </summary>
	/// <returns>Notes per second of the chart including the notes from the patterns for this action.</returns>
	private double GetNps()
	{
		var numSteps = EditorChart.GetStepCount() + GetTotalStepsFromAllPatterns();
		var startTime = EditorChart.GetStartChartTime();
		var endTime = Math.Max(GetLastPatternStepTime(), EditorChart.GetEndChartTime());
		var totalTime = endTime - startTime;
		return totalTime > 0.0 ? numSteps / totalTime : 0.0;
	}

	/// <summary>
	/// Gets the total number of steps which will be generated by all patterns for this action.
	/// </summary>
	/// <returns>Total number of steps which will be generated by all patterns for this action.</returns>
	private int GetTotalStepsFromAllPatterns()
	{
		var steps = 0;
		for (var patternIndex = 0; patternIndex < Patterns.Count; patternIndex++)
		{
			// Get the row the next pattern starts at in case it cuts off this pattern.
			var nextPatternStartRow = -1;
			var nextPatternIndex = patternIndex + 1;
			if (nextPatternIndex < Patterns.Count)
			{
				if (Patterns[nextPatternIndex].GetNumSteps() > 0)
				{
					nextPatternStartRow = Patterns[nextPatternIndex].GetFirstStepRow();
				}
			}

			// Add the events from this pattern which won't be cut off by the following pattern.
			if (nextPatternStartRow != -1)
				steps += Patterns[patternIndex].GetNumStepsBeforeRow(nextPatternStartRow);
			else
				steps += Patterns[patternIndex].GetNumSteps();
		}

		return steps;
	}

	/// <summary>
	/// Gets the time of the last step to be generated by the patterns for this action.
	/// </summary>
	/// <returns>Time of the last step to be generated by the patterns for this action.</returns>
	private double GetLastPatternStepTime()
	{
		var lastStepTime = 0.0;

		// For overlapping patterns we intentionally only generate up to the end of the last pattern.
		for (var patternIndex = Patterns.Count - 1; patternIndex >= 0; patternIndex--)
		{
			if (Patterns[patternIndex].GetNumSteps() > 0)
			{
				var lastStepRow = Patterns[patternIndex].GetLastStepRow();
				if (EditorChart.TryGetTimeFromChartPosition(lastStepRow, ref lastStepTime))
					return lastStepTime;
			}
		}

		return lastStepTime;
	}
}
