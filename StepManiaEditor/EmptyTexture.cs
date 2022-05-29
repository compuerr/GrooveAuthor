﻿using System;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	/// <summary>
	/// A MonoGame Texture to render via ImGui representing an unloaded, empty texture with a checkerboard pattern.
	/// </summary>
	class EmptyTexture : IDisposable
	{
		private const int CheckerDimension = 32;
		private const uint DarkColor = 0xFF1E1E1E;
		private const uint LightColor = 0xFF303030;

		private readonly ImGuiRenderer ImGuiRenderer;
		private Texture2D TextureMonogame;
		private readonly IntPtr TextureImGui;
		private readonly string ImGuiId;
		private readonly uint Width;
		private readonly uint Height;
		private bool Disposed;

		private static int Id;

		public EmptyTexture(GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer, uint width, uint height)
		{
			ImGuiId = $"EmptyTexture{Id++}";
			Width = width;
			Height = height;
			ImGuiRenderer = imGuiRenderer;
			TextureMonogame = CreateEmptyTexture(graphicsDevice);
			TextureImGui = ImGuiRenderer.BindTexture(TextureMonogame);
		}

		~EmptyTexture()
		{
			Dispose();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (Disposed)
				return;

			if (disposing)
			{
				ImGuiRenderer.UnbindTexture(TextureImGui);
				TextureMonogame = null;
			}

			Disposed = true;
		}

		/// <summary>
		/// Draw the empty texture as an image through ImGui.
		/// </summary>
		/// <param name="mode">TextureLayoutMode for how to layout this texture.</param>
		public void Draw(Utils.TextureLayoutMode mode = Utils.TextureLayoutMode.Box)
		{
			Utils.DrawImage(ImGuiId, TextureImGui, TextureMonogame, Width, Height, mode);
		}

		/// <summary>
		/// Draw the empty texture as a button through ImGui.
		/// </summary>
		/// <param name="mode">TextureLayoutMode for how to layout this texture.</param>
		/// <returns>True if the button was pressed and false otherwise.</returns>
		public bool DrawButton(Utils.TextureLayoutMode mode = Utils.TextureLayoutMode.Box)
		{
			return Utils.DrawButton(ImGuiId, TextureImGui, TextureMonogame, Width, Height, mode);
		}

		/// <summary>
		/// Creates the empty texture by filling a new Texture2D with a checkerboard pattern.
		/// </summary>
		/// <param name="graphicsDevice">GraphicsDevice for creating the texture.</param>
		/// <returns>Newly created texture</returns>
		private Texture2D CreateEmptyTexture(GraphicsDevice graphicsDevice)
		{
			var tex = new Texture2D(graphicsDevice, (int)Width, (int)Height);
			var textureData = new uint[Width * Height];
			var flipPattern = false;
			var dark = true;
			for (var x = 0; x < Width; x++)
			{
				if (x % CheckerDimension == 0)
					flipPattern = !flipPattern;

				for (var y = 0; y < Height; y++)
				{
					if (y == 0)
						dark = flipPattern;
					if (y % CheckerDimension == 0)
						dark = !dark;
					textureData[y * Width + x] = dark ? DarkColor : LightColor;
				}
			}
			tex.SetData(textureData);
			return tex;
		}
	}
}
