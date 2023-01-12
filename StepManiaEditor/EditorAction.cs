﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Fumen.ChartDefinition;
using StepManiaLibrary;
using static Fumen.Converters.SMCommon;
using static System.Diagnostics.Debug;

namespace StepManiaEditor
{
	/// <summary>
	/// An action that can be done and undone.
	/// Meant to be used by ActionQueue.
	/// </summary>
	internal abstract class EditorAction
	{
		/// <summary>
		/// Do the action.
		/// </summary>
		public abstract void Do();

		/// <summary>
		/// Undo the action.
		/// </summary>
		public abstract void Undo();

		/// <summary>
		/// Returns whether or not this action represents a change to the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		public abstract bool AffectsFile();

		/// <summary>
		/// Returns how many actions up to and including this action affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		/// <returns></returns>
		public int GetTotalNumActionsAffectingFile()
		{
			return NumPreviousActionsAffectingFile + (AffectsFile() ? 1 : 0);
		}

		/// <summary>
		/// Sets the number of previous actions which affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		public void SetNumPreviousActionsAffectingFile(int actions)
		{
			NumPreviousActionsAffectingFile = actions;
		}

		/// <summary>
		/// Number of previous actions which affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		protected int NumPreviousActionsAffectingFile = 0;
	}

	/// <summary>
	/// EditorAction to set a Field or a Property for a value type on an object.
	/// </summary>
	/// <typeparam name="T">
	/// Reference type of object field or property.
	/// </typeparam>
	internal sealed class ActionSetObjectFieldOrPropertyValue<T> : EditorAction where T : struct
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly object O;
		private readonly string FieldOrPropertyName;
		private readonly bool IsField;
		private readonly FieldInfo FieldInfo;
		private readonly PropertyInfo PropertyInfo;
		private readonly bool DoesAffectFile;

		/// <summary>
		/// Constructor with a given value to set.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o">Object to modify.</param>
		/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
		/// <param name="value">New value to set.</param>
		public ActionSetObjectFieldOrPropertyValue(object o, string fieldOrPropertyName, T value, bool affectsFile)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			PreviousValue = IsField ? (T)FieldInfo.GetValue(O) : (T)PropertyInfo.GetValue(O);

			DoesAffectFile = affectsFile;
		}

		/// <summary>
		/// Constructor with a given value and previous value to set.
		/// It is assumed value is a Clone of the value.
		/// It is assumed previousValue is a Clone of the previous value.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o"></param>
		/// <param name="fieldOrPropertyName"></param>
		/// <param name="value"></param>
		/// <param name="previousValue"></param>
		public ActionSetObjectFieldOrPropertyValue(object o, string fieldOrPropertyName, T value, T previousValue, bool affectsFile)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			PreviousValue = previousValue;

			DoesAffectFile = affectsFile;
		}

		public override bool AffectsFile()
		{
			return DoesAffectFile;
		}

		public override string ToString()
		{
			return $"Set {O.GetType()} {FieldOrPropertyName} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			// Set Value on O.
			if (IsField)
				FieldInfo.SetValue(O, Value);
			else
				PropertyInfo.SetValue(O, Value);
		}

		public override void Undo()
		{
			// Set PreviousValue on O.
			if (IsField)
				FieldInfo.SetValue(O, PreviousValue);
			else
				PropertyInfo.SetValue(O, PreviousValue);
		}
	}

	/// <summary>
	/// EditorAction to set a Field or a Property for a reference type on an object.
	/// </summary>
	/// <typeparam name="T">
	/// Reference type of object field or property.
	/// Must be Cloneable to ensure save undo and redo operations.
	/// </typeparam>
	internal sealed class ActionSetObjectFieldOrPropertyReference<T> : EditorAction where T : class, ICloneable
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly object O;
		private readonly string FieldOrPropertyName;
		private readonly bool IsField;
		private readonly FieldInfo FieldInfo;
		private readonly PropertyInfo PropertyInfo;
		private readonly bool DoesAffectFile;

		/// <summary>
		/// Constructor with a given value to set.
		/// It is assumed value is a Clone of the value.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o">Object to modify.</param>
		/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
		/// <param name="value">New value to set.</param>
		public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value, bool affectsFile)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			// Clone the previous value.
			PreviousValue = IsField ? (T)FieldInfo.GetValue(O) : (T)PropertyInfo.GetValue(O);
			PreviousValue = (T)PreviousValue.Clone();

			DoesAffectFile = affectsFile;
		}

		/// <summary>
		/// Constructor with a given value and previous value to set.
		/// It is assumed value is a Clone of the value.
		/// It is assumed previousValue is a Clone of the previous value.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o"></param>
		/// <param name="fieldOrPropertyName"></param>
		/// <param name="value"></param>
		/// <param name="previousValue"></param>
		public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value, T previousValue, bool affectsFile)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			PreviousValue = previousValue;

			DoesAffectFile = affectsFile;
		}

		public override bool AffectsFile()
		{
			return DoesAffectFile;
		}

		public override string ToString()
		{
			return $"Set {O.GetType()} {FieldOrPropertyName} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			// Clone Value to O.
			if (IsField)
				FieldInfo.SetValue(O, (T)Value.Clone());
			else
				PropertyInfo.SetValue(O, (T)Value.Clone());
		}

		public override void Undo()
		{
			// Clone PreviousValue to O.
			if (IsField)
				FieldInfo.SetValue(O, (T)PreviousValue.Clone());
			else
				PropertyInfo.SetValue(O, (T)PreviousValue.Clone());
		}
	}

	internal sealed class ActionSetExtrasValue<T> : EditorAction
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly bool PreviousValueSet;
		private readonly Extras Extras;
		private readonly string ExtrasKey;
		private readonly string LogType;

		public ActionSetExtrasValue(string logType, Extras extras, string extrasKey, T value)
		{
			LogType = logType;
			Extras = extras;
			Value = value;
			ExtrasKey = extrasKey;
			PreviousValueSet = Extras.TryGetSourceExtra(ExtrasKey, out PreviousValue);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Set {LogType} {ExtrasKey} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			Extras.AddSourceExtra(ExtrasKey, Value, true);
		}

		public override void Undo()
		{
			if (PreviousValueSet)
				Extras.AddSourceExtra(ExtrasKey, PreviousValue, true);
			else
				Extras.RemoveSourceExtra(ExtrasKey);
		}
	}

	/// <summary>
	/// EditorAction for changing the ShouldAllowEditsOfMax field of a DisplayTempo.
	/// When disabling ShouldAllowEditsOfMax, the max tempo is forced to be the min.
	/// If they were different before setting ShouldAllowEditsOfMax to true, then undoing
	/// that change should restore the max tempo back to what it was previously.
	/// </summary>
	internal sealed class ActionSetDisplayTempoAllowEditsOfMax : EditorAction
	{
		private readonly DisplayTempo DisplayTempo;
		private readonly double PreviousMax;
		private readonly bool Allow;

		public ActionSetDisplayTempoAllowEditsOfMax(DisplayTempo displayTempo, bool allow)
		{
			DisplayTempo = displayTempo;
			PreviousMax = DisplayTempo.SpecifiedTempoMax;
			Allow = allow;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Set display tempo ShouldAllowEditsOfMax '{!Allow}' > '{Allow}'.";
		}

		public override void Do()
		{
			DisplayTempo.ShouldAllowEditsOfMax = Allow;
			if (!DisplayTempo.ShouldAllowEditsOfMax)
				DisplayTempo.SpecifiedTempoMax = DisplayTempo.SpecifiedTempoMin;
		}

		public override void Undo()
		{
			DisplayTempo.ShouldAllowEditsOfMax = !Allow;
			if (DisplayTempo.ShouldAllowEditsOfMax)
				DisplayTempo.SpecifiedTempoMax = PreviousMax;
		}
	}

	internal sealed class ActionMultiple : EditorAction
	{
		private readonly List<EditorAction> Actions;

		public ActionMultiple()
		{
			Actions = new List<EditorAction>();
		}

		public ActionMultiple(List<EditorAction> actions)
		{
			Actions = actions;
		}

		public void EnqueueAndDo(EditorAction action)
		{
			action.Do();
			Actions.Add(action);
		}

		public void EnqueueWithoutDoing(EditorAction action)
		{
			Actions.Add(action);
		}

		public List<EditorAction> GetActions()
		{
			return Actions;
		}

		public override bool AffectsFile()
		{
			foreach (var action in Actions)
			{
				if (action.AffectsFile())
					return true;
			}
			return false;
		}

		public override string ToString()
		{
			return string.Join(' ', Actions);
		}

		public override void Do()
		{
			foreach (var action in Actions)
			{
				action.Do();
			}
		}

		public override void Undo()
		{
			var i = Actions.Count - 1;
			while (i >= 0)
			{
				Actions[i--].Undo();
			}
		}

		public void Clear()
		{
			Actions.Clear();
		}
	}

	internal sealed class ActionAddEditorEvent : EditorAction
	{
		private EditorEvent EditorEvent;

		public ActionAddEditorEvent(EditorEvent editorEvent)
		{
			EditorEvent = editorEvent;
		}

		public void UpdateEvent(EditorEvent editorEvent)
		{
			EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
			EditorEvent = editorEvent;
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}

		public void SetIsBeingEdited(bool isBeingEdited)
		{
			EditorEvent.SetIsBeingEdited(isBeingEdited);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			// TODO: Nice strings
			return $"Add {EditorEvent.GetType()}.";
		}

		public override void Do()
		{
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}

		public override void Undo()
		{
			EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
		}
	}

	internal sealed class ActionDeleteEditorEvents : EditorAction
	{
		private readonly List<EditorEvent> EditorEvents = new List<EditorEvent>();

		/// <summary>
		/// Deleting an event may result in other events also being deleted.
		/// We store all deleted events as a result of the requested delete so
		/// that when we redo the action we can restore them all.
		/// </summary>
		private List<EditorEvent> AllDeletedEvents = new List<EditorEvent>();

		public ActionDeleteEditorEvents(EditorEvent editorEvent)
		{
			EditorEvents.Add(editorEvent);
		}

		public ActionDeleteEditorEvents(List<EditorEvent> editorEvents, bool copy)
		{
			if (copy)
				EditorEvents.AddRange(editorEvents);
			else
				EditorEvents = editorEvents;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			// TODO: Nice strings
			var count = EditorEvents.Count;
			if (count == 1)
			{
				return $"Delete {EditorEvents[0].GetType()}.";
			}
			return $"Delete {count} events.";
		}

		public override void Do()
		{
			AllDeletedEvents = EditorEvents[0].GetEditorChart().DeleteEvents(EditorEvents);
		}

		public override void Undo()
		{
			EditorEvents[0].GetEditorChart().AddEvents(AllDeletedEvents);
		}
	}

	internal sealed class ActionChangeHoldLength : EditorAction
	{
		private EditorHoldStartNoteEvent HoldStart;
		private EditorHoldEndNoteEvent HoldEnd;
		private EditorHoldEndNoteEvent NewHoldEnd;

		public ActionChangeHoldLength(EditorHoldStartNoteEvent holdStart, int length)
		{
			var newHoldEndRow = holdStart.GetRow() + length;
			var newHoldEndTime = 0.0;
			holdStart.GetEditorChart().TryGetTimeFromChartPosition(newHoldEndRow, ref newHoldEndTime);

			HoldStart = holdStart;
			HoldEnd = holdStart.GetHoldEndNote();
			var holdEndNote = new LaneHoldEndNote()
			{
				Lane = HoldStart.GetLane(),
				IntegerPosition = HoldStart.GetRow() + length,
				TimeMicros = Fumen.Utils.ToMicrosRounded(newHoldEndTime)
			};
			var config = new EditorEvent.EventConfig
			{
				EditorChart = HoldStart.GetEditorChart(),
				ChartEvent = holdEndNote
			};
			NewHoldEnd = new EditorHoldEndNoteEvent(config, holdEndNote);
			NewHoldEnd.SetHoldStartNote(HoldStart);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = HoldStart.IsRoll() ? "roll" : "hold";
			return $"Change {typeStr} length from to {HoldEnd.GetRow() - HoldStart.GetRow()} to {NewHoldEnd.GetRow() - HoldStart.GetRow()}.";
		}

		public override void Do()
		{
			HoldStart.GetEditorChart().DeleteEvent(HoldEnd);
			HoldStart.SetHoldEndNote(NewHoldEnd);
			HoldStart.GetEditorChart().AddEvent(NewHoldEnd);
		}

		public override void Undo()
		{
			HoldStart.GetEditorChart().DeleteEvent(NewHoldEnd);
			HoldStart.SetHoldEndNote(HoldEnd);
			HoldStart.GetEditorChart().AddEvent(HoldEnd);
		}
	}

	internal sealed class ActionAddHoldEvent : EditorAction
	{
		private EditorHoldStartNoteEvent HoldStart;
		private EditorHoldEndNoteEvent HoldEnd;

		public ActionAddHoldEvent(EditorChart chart, int lane, int row, int length, bool roll, bool isBeingEdited)
		{
			(HoldStart, HoldEnd) = EditorHoldStartNoteEvent.CreateHold(chart, lane, row, length, roll);
			HoldStart.SetIsBeingEdited(isBeingEdited);
			HoldEnd.SetIsBeingEdited(isBeingEdited);
		}

		public EditorHoldStartNoteEvent GetHoldStartEvent()
		{
			return HoldStart;
		}

		public void SetIsRoll(bool roll)
		{
			HoldStart.SetIsRoll(roll);
			HoldEnd.SetIsRoll(roll);
		}

		public void SetIsBeingEdited(bool isBeingEdited)
		{
			HoldStart.SetIsBeingEdited(isBeingEdited);
			HoldEnd.SetIsBeingEdited(isBeingEdited);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = HoldStart.IsRoll() ? "roll" : "hold";
			return $"Add {typeStr}.";
		}

		public override void Do()
		{
			HoldStart.GetEditorChart().AddEvent(HoldStart);
			HoldStart.GetEditorChart().AddEvent(HoldEnd);
		}

		public override void Undo()
		{
			HoldStart.GetEditorChart().DeleteEvent(HoldStart);
			HoldStart.GetEditorChart().DeleteEvent(HoldEnd);
		}
	}

	internal sealed class ActionChangeHoldType : EditorAction
	{
		private bool Roll;
		private EditorHoldStartNoteEvent HoldStart;

		public ActionChangeHoldType(EditorHoldStartNoteEvent holdStart, bool roll)
		{
			HoldStart = holdStart;
			Roll = roll;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var originalType = Roll ? "hold" : "roll";
			var newType = Roll ? "roll" : "hold";
			return $"Change {originalType} to {newType}.";
		}

		public override void Do()
		{
			HoldStart.SetIsRoll(Roll);
		}

		public override void Undo()
		{
			HoldStart.SetIsRoll(!Roll);
		}
	}

	internal sealed class ActionDeleteHoldEvent : EditorAction
	{
		private EditorHoldStartNoteEvent HoldStart;

		public ActionDeleteHoldEvent(EditorHoldStartNoteEvent holdStart)
		{
			HoldStart = holdStart;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = HoldStart.IsRoll() ? "roll" : "hold";
			return $"Delete {typeStr}.";
		}

		public override void Do()
		{
			HoldStart.GetEditorChart().DeleteEvent(HoldStart);
			HoldStart.GetEditorChart().DeleteEvent(HoldStart.GetHoldEndNote());
		}

		public override void Undo()
		{
			HoldStart.GetEditorChart().AddEvent(HoldStart);
			HoldStart.GetEditorChart().AddEvent(HoldStart.GetHoldEndNote());
		}
	}

	internal sealed class ActionSelectChart : EditorAction
	{
		private Editor Editor;
		private EditorChart Chart;
		private EditorChart PreviousChart;

		public ActionSelectChart(Editor editor, EditorChart chart)
		{
			Editor = editor;
			PreviousChart = Editor.GetActiveChart();
			Chart = chart;
		}

		public override bool AffectsFile()
		{
			return false;
		}

		public override string ToString()
		{
			return $"Select {Utils.GetPrettyEnumString(Chart.ChartType)} {Utils.GetPrettyEnumString(Chart.ChartDifficultyType)} Chart.";
		}

		public override void Do()
		{
			Editor.OnChartSelected(Chart, false);
		}

		public override void Undo()
		{
			Editor.OnChartSelected(PreviousChart, false);
		}
	}

	internal sealed class ActionAddChart : EditorAction
	{
		private Editor Editor;
		private ChartType ChartType;
		private EditorChart AddedChart;
		private EditorChart PreivouslyActiveChart;

		public ActionAddChart(Editor editor, ChartType chartType)
		{
			Editor = editor;
			ChartType = chartType;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Add {Utils.GetPrettyEnumString(ChartType)} Chart.";
		}

		public override void Do()
		{
			PreivouslyActiveChart = Editor.GetActiveChart();
			
			// Through undoing and redoing we may add the same chart multiple times.
			// Other actions like ActionAddEditorEvent reference specific charts.
			// For those actions to work as expected we should restore the same chart instance
			// rather than creating a new one when undoing and redoing.
			if (AddedChart != null)
				Editor.AddChart(AddedChart, true);
			else
				AddedChart = Editor.AddChart(ChartType, true);
		}

		public override void Undo()
		{
			Editor.DeleteChart(AddedChart, PreivouslyActiveChart);
		}
	}

	internal sealed class ActionDeleteChart : EditorAction
	{
		private Editor Editor;
		private EditorChart Chart;
		private bool DeletedActiveChart;

		public ActionDeleteChart(Editor editor, EditorChart chart)
		{
			Editor = editor;
			Chart = chart;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Delete {Utils.GetPrettyEnumString(Chart.ChartType)} Chart.";
		}

		public override void Do()
		{
			DeletedActiveChart = Editor.GetActiveChart() == Chart;
			Editor.DeleteChart(Chart, null);
		}

		public override void Undo()
		{
			Editor.AddChart(Chart, DeletedActiveChart);
		}
	}

	internal sealed class ActionMoveFocalPoint : EditorAction
	{
		private int PreviousX;
		private int PreviousY;
		private int NewX;
		private int NewY;

		public ActionMoveFocalPoint(int previousX, int previousY, int newX, int newY)
		{
			PreviousX = previousX;
			PreviousY = previousY;
			NewX = newX;
			NewY = newY;
		}

		public override bool AffectsFile()
		{
			return false;
		}

		public override string ToString()
		{
			return $"Move receptors from ({PreviousX}, {PreviousY}) to ({NewX}, {NewY}).";
		}

		public override void Do()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = NewX;
			Preferences.Instance.PreferencesReceptors.PositionY = NewY;
		}

		public override void Undo()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = PreviousX;
			Preferences.Instance.PreferencesReceptors.PositionY = PreviousY;
		}
	}

	internal abstract class ActionTransformSelection : EditorAction
	{
		private Editor Editor;
		private EditorChart Chart;
		private List<EditorEvent> OriginalEvents;
		private List<EditorEvent> DeletedFromAlteration;
		private List<EditorEvent> AddedFromAlteration;

		public ActionTransformSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
		{
			Editor = editor;
			Chart = chart;

			// Copy the given events.
			var padData = Editor.GetPadData(Chart.ChartType);
			OriginalEvents = new List<EditorEvent>();
			if (padData != null)
			{
				foreach (var chartEvent in events)
				{
					if (!CanTransform(chartEvent, padData))
						continue;
					OriginalEvents.Add(chartEvent);
				}
				OriginalEvents.Sort();
			}
		}

		public override bool AffectsFile()
		{
			return true;
		}

		protected abstract bool CanTransform(EditorEvent e, PadData padData);
		protected abstract void DoTransform(EditorEvent e, PadData padData);
		protected abstract void UndoTransform(EditorEvent e, PadData padData);

		public override void Do()
		{
			var padData = Editor.GetPadData(Chart.ChartType);
			if (padData == null)
				return;

			// When starting a transformation let the Editor know.
			Editor.OnNoteTransformationBegin();

			// Remove all events to be transformed.
			var allDeletedEvents = Chart.DeleteEvents(OriginalEvents);
			Assert(allDeletedEvents.Count == OriginalEvents.Count);

			// Transform events.
			foreach (var editorEvent in OriginalEvents)
				DoTransform(editorEvent, padData);

			// Add the events back, storing the side effects.
			(AddedFromAlteration, DeletedFromAlteration) = Chart.ForceAddEvents(OriginalEvents);

			// Notify the Editor the transformation is complete.
			Editor.OnNoteTransformationEnd();
		}

		public override void Undo()
		{
			// When starting a transformation let the Editor know.
			Editor.OnNoteTransformationBegin();

			// Remove the transformed events.
			var allDeletedEvents = Chart.DeleteEvents(OriginalEvents);
			Assert(allDeletedEvents.Count == OriginalEvents.Count);

			// While the transformed events are removed, delete the events which
			// were added as a side effect.
			if (AddedFromAlteration.Count > 0)
			{
				allDeletedEvents = Chart.DeleteEvents(AddedFromAlteration);
				Assert(allDeletedEvents.Count == AddedFromAlteration.Count);
			}

			// While the transformed events are removed, add the events which
			// were deleted as a side effect.
			if (DeletedFromAlteration.Count > 0)
			{
				Chart.AddEvents(DeletedFromAlteration);
			}

			// Undo the transformation on each event.
			var padData = Editor.GetPadData(Chart.ChartType);
			foreach (var editorEvent in OriginalEvents)
				UndoTransform(editorEvent, padData);

			// Add the events back.
			Chart.AddEvents(OriginalEvents);

			// Notify the Editor the transformation is complete.
			Editor.OnNoteTransformationEnd();
		}
	}

	internal sealed class ActionMirrorSelection : ActionTransformSelection
	{
		public ActionMirrorSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
			: base(editor, chart, events)
		{	
		}

		public override string ToString()
		{
			return $"Mirror Notes.";
		}

		protected override bool CanTransform(EditorEvent e, PadData padData)
		{
			var lane = e.GetLane();
			if (lane == Constants.InvalidArrowIndex)
				return false;
			if (padData.ArrowData[lane].MirroredLane == Constants.InvalidArrowIndex)
				return false;
			if (lane == padData.ArrowData[lane].MirroredLane)
				return false;
			return true;
		}

		protected override void DoTransform(EditorEvent e, PadData padData)
		{
			e.SetLane(padData.ArrowData[e.GetLane()].MirroredLane);
		}

		protected override void UndoTransform(EditorEvent e, PadData padData)
		{
			DoTransform(e, padData);
		}

	}

	internal sealed class ActionFlipSelection : ActionTransformSelection
	{

		public ActionFlipSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
			: base(editor, chart, events)
		{
		}

		public override string ToString()
		{
			return $"Flip Notes.";
		}

		protected override bool CanTransform(EditorEvent e, PadData padData)
		{
			var lane = e.GetLane();
			if (lane == Constants.InvalidArrowIndex)
				return false;
			if (padData.ArrowData[lane].FlippedLane == Constants.InvalidArrowIndex)
				return false;
			if (lane == padData.ArrowData[lane].FlippedLane)
				return false;
			return true;
		}

		protected override void DoTransform(EditorEvent e, PadData padData)
		{
			e.SetLane(padData.ArrowData[e.GetLane()].FlippedLane);
		}

		protected override void UndoTransform(EditorEvent e, PadData padData)
		{
			DoTransform(e, padData);
		}
	}

	internal sealed class ActionMirrorAndFlipSelection : ActionTransformSelection
	{
		
		public ActionMirrorAndFlipSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
			: base(editor, chart, events)
		{
		}

		public override string ToString()
		{
			return $"Mirror and Flip Notes.";
		}

		protected override bool CanTransform(EditorEvent e, PadData padData)
		{
			var lane = e.GetLane();
			if (lane == Constants.InvalidArrowIndex)
				return false;
			var transformedLane = padData.ArrowData[lane].MirroredLane;
			if (transformedLane == Constants.InvalidArrowIndex)
				return false;
			transformedLane = padData.ArrowData[transformedLane].FlippedLane;
			if (transformedLane == Constants.InvalidArrowIndex)
				return false;
			if (lane == transformedLane)
				return false;
			return true;
		}

		protected override void DoTransform(EditorEvent e, PadData padData)
		{
			e.SetLane(padData.ArrowData[padData.ArrowData[e.GetLane()].MirroredLane].FlippedLane);
		}

		protected override void UndoTransform(EditorEvent e, PadData padData)
		{
			DoTransform(e, padData);
		}
	}
}
