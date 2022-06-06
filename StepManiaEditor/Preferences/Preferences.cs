﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using Fumen.Converters;

namespace StepManiaEditor
{
	/// <summary>
	/// Miscellaneous preferences to save to disk.
	/// </summary>
	public class Preferences
	{
		/// <summary>
		/// File to use for deserializing Preferences.
		/// </summary>
		private const string FileName = "Preferences.json";

		/// <summary>
		/// Serialization options.
		/// </summary>
		private static JsonSerializerOptions SerializationOptions = new JsonSerializerOptions()
		{
			Converters =
			{
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
			},
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			IncludeFields = true,
			WriteIndented = true
		};

		public class SavedSongInformation
		{
			[JsonInclude] public string FileName;
			[JsonInclude] public SMCommon.ChartType LastChartType;
			[JsonInclude] public SMCommon.ChartDifficultyType LastChartDifficultyType;
			// TODO: Zoom level and position
		}

		/// <summary>
		/// Static Config instance.
		/// </summary>
		public static Preferences Instance { get; private set; } = new Preferences();

		// Window preferences
		[JsonInclude] public int WindowWidth = 1920;
		[JsonInclude] public int WindowHeight = 1080;
		[JsonInclude] public bool WindowFullScreen = false;
		[JsonInclude] public bool WindowMaximized = false;

		// Waveform preferences
		[JsonInclude] public PreferencesWaveForm PreferencesWaveForm = new PreferencesWaveForm();

		// Scroll control preferences
		[JsonInclude] public PreferencesScroll PreferencesScroll = new PreferencesScroll();

		// MiniMap preferences
		[JsonInclude] public PreferencesMiniMap PreferencesMiniMap = new PreferencesMiniMap();

		// Option preferences
		[JsonInclude] public PreferencesOptions PreferencesOptions = new PreferencesOptions();

		// Log preferences
		[JsonInclude] public bool ShowLogWindow = true;
		[JsonInclude] public int LogWindowDateDisplay = 1;
		[JsonInclude] public LogLevel LogWindowLevel = LogLevel.Info;
		[JsonInclude] public bool LogWindowLineWrap;

		// Misc
		[JsonInclude] public bool ShowSongPropertiesWindow = false;
		[JsonInclude] public bool ShowChartPropertiesWindow = false;
		[JsonInclude] public string OpenFileDialogInitialDirectory = @"C:\Games\StepMania 5\Songs\";
		[JsonInclude] public List<SavedSongInformation> RecentFiles = new List<SavedSongInformation>();

		// Debug
		[JsonInclude] public double DebugSongTime = 0.0;
		[JsonInclude] public double DebugZoom = 1.0;

		/// <summary>
		/// Public Constructor.
		/// This should be private but it needs to be public for JSON deserialization.
		/// </summary>
		public Preferences()
		{
			PostLoad();
		}

		private void PostLoad()
		{
			PreferencesOptions.PostLoad();
		}

		private void PreSave()
		{
			PreferencesOptions.PreSave();
		}

		/// <summary>
		/// Loads the Preferences from the preferences json file.
		/// </summary>
		/// <returns>Preferences Instance.</returns>
		public static async Task<Preferences> LoadAsync()
		{
			Logger.Info($"Loading {FileName}...");

			try
			{
				using (FileStream openStream = File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)))
				{
					Instance = await JsonSerializer.DeserializeAsync<Preferences>(openStream, SerializationOptions);
					Instance.PostLoad();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {FileName}. {e}");
				return Instance;
			}

			Logger.Info($"Loaded {FileName}.");
			return Instance;
		}

		/// <summary>
		/// Loads the Preferences from the preferences json file.
		/// </summary>
		/// <returns>Preferences Instance.</returns>
		public static Preferences Load()
		{
			Logger.Info($"Loading {FileName}...");

			try
			{
				using (FileStream openStream = File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)))
				{
					Instance = JsonSerializer.Deserialize<Preferences>(openStream, SerializationOptions);
					Instance.PostLoad();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {FileName}. {e}");
				return Instance;
			}

			Logger.Info($"Loaded {FileName}.");
			return Instance;
		}

		/// <summary>
		/// Save the Preferences to the preferences json file.
		/// </summary>
		public static async Task SaveAsync()
		{
			Logger.Info($"Saving {FileName}...");

			try
			{
				Instance.PreSave();
				var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
				await File.WriteAllTextAsync(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName), jsonString);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to save {FileName}. {e}");
				return;
			}

			Logger.Info($"Saved {FileName}.");
		}

		/// <summary>
		/// Save the Preferences to the preferences json file.
		/// </summary>
		public static void Save()
		{
			Logger.Info($"Saving {FileName}...");

			try
			{
				Instance.PreSave();
				var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
				File.WriteAllText(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName), jsonString);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to save {FileName}. {e}");
				return;
			}

			Logger.Info($"Saved {FileName}.");
		}
	}
}