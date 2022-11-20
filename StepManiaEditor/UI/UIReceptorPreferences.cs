﻿using System.Numerics;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing receptor preferences UI.
	/// </summary>
	public class UIReceptorPreferences
	{
		private readonly Editor Editor;

		public UIReceptorPreferences(Editor editor)
		{
			Editor = editor;
		}

		public void Draw()
		{
			var p = Preferences.Instance.PreferencesReceptors;
			if (!p.ShowReceptorPreferencesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Receptor Preferences", ref p.ShowReceptorPreferencesWindow, ImGuiWindowFlags.NoScrollbar);

			ImGui.Text("Position");
			if (ImGuiLayoutUtils.BeginTable("Receptor Placement", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Center Horizontally", p, nameof(PreferencesReceptors.CenterHorizontally),
					"Whether to keep the receptors centered horizontally in the window.");

				ImGuiLayoutUtils.DrawRowDragInt2(true, "Position", p, nameof(PreferencesReceptors.PositionX), nameof(PreferencesReceptors.PositionY), !p.CenterHorizontally, true,
					"Position of the receptors."
					+ "\nThe receptors can also be moved by dragging them with the left mouse button."
					+ "\nHold shift while dragging to limit movement to one dimension.", 1.0f, "%i", 0, Editor.GetViewportWidth() - 1, 0, Editor.GetViewportHeight() - 1);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Animation Misc");
			if (ImGuiLayoutUtils.BeginTable("Receptor Animation Misc", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Pulse Receptors", p, nameof(PreferencesReceptors.PulseReceptorsWithTempo),
					"Whether to pulse the receptors to the chart tempo.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Autoplay Animations");
			if (ImGuiLayoutUtils.BeginTable("Receptor Animation Autoplay", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Hide Arrows", p, nameof(PreferencesReceptors.AutoPlayHideArrows),
					"When playing, whether to hide the arrows after they pass the receptors.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Light Holds", p, nameof(PreferencesReceptors.AutoPlayLightHolds),
					"When playing, whether to highlight hold and roll notes when they would be active.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Rim Effect", p, nameof(PreferencesReceptors.AutoPlayRimEffect),
					"When playing, whether to show a rim effect on the receptors from simulated input.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Glow Effect", p, nameof(PreferencesReceptors.AutoPlayGlowEffect),
					"When playing, whether to show a glow effect on the receptors from simulated input.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Shrink Effect", p, nameof(PreferencesReceptors.AutoPlayShrinkEffect),
					"When playing, whether to shrink the receptors from simulated input.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Input Animations");
			if (ImGuiLayoutUtils.BeginTable("Receptor Animation Input", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Rim Effect", p, nameof(PreferencesReceptors.TapRimEffect),
					"When tapping an arrow, whether to show a rim effect on the receptors.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Shrink Effect", p, nameof(PreferencesReceptors.TapShrinkEffect),
					"When tapping an arrow, whether to shrink the receptors.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Receptor Animation Restore", 120))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all animation preferences to their default values."))
				{
					p.RestoreDefaults();
				}
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.End();
		}
	}
}