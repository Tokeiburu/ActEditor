﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.IO;
using GRF.Image;
using GRF.System;
using GrfToWpfBridge.Application;
using TokeiLibrary;
using Utilities;

namespace ActEditor {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		public App() {
			Configuration.ConfigAsker = ActEditorConfiguration.ConfigAsker;
			ErrorHandler.SetErrorHandler(new DefaultErrorHandler());
			Settings.TempPath = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "tmp");
			TemporaryFilesManager.ClearTemporaryFiles();
			SelfPatcher.SelfPatch();
			Spr.AutomaticDowngradeOnRleException = true;
			Configuration.ProgramDataPath = GrfPath.Combine(Configuration.ApplicationDataPath, ActEditorConfiguration.ProgramName);
			EffectConfiguration.ConfigAsker = ActEditorConfiguration.ConfigAsker;
			EffectConfiguration.DisplayAction = (effectConfig, act, actionIndex) => {
				EffectPreviewDialog effectDialog = new EffectPreviewDialog(act, actionIndex, effectConfig);
				EffectConfiguration.Displayed = true;

				effectDialog.Closed += delegate {
					EffectConfiguration.Displayed = false;
				};

				effectDialog.ShowDialog();
			};
		}

		protected override void OnStartup(StartupEventArgs e) {
			ApplicationManager.CrashReportEnabled = true;
			ImageConverterManager.AddConverter(new DefaultImageConverter());

			Configuration.SetImageRendering(Resources);

			Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/GRFEditorStyles.xaml", UriKind.RelativeOrAbsolute) });

			if (ActEditorConfiguration.StyleTheme == "") {
				
			}
			else {
				Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/StyleDark.xaml", UriKind.RelativeOrAbsolute) });

				ApplicationManager.ImageProcessing = delegate(string name, BitmapFrame img) {
					if (img == null) return null;

					if (name.Contains("reset.png")) {
						Func<byte, byte, byte, byte, Color> shader = delegate(byte A, byte R, byte G, byte B) {
							return Color.FromArgb(A, _clamp((R) * 1.8), _clamp(G / 3), _clamp(B / 3));
						};

						return _applyShader(img, shader);
					}
					else if (name.Contains("eye.png") || name.Contains("smallArrow.png") || name.Contains("cs_pen.png") || name.Contains("cs_eraser.png")) {
						Func<byte, byte, byte, byte, Color> shader = delegate(byte A, byte R, byte G, byte B) {
							return Color.FromArgb(A, _clamp((255 - R) * 0.8), _clamp((255 - G) * 0.8), _clamp((255 - B) * 0.8));
						};

						return _applyShader(img, shader);
					}
					else if (name.Contains("arrow.png") ||
							 name.Contains("arrowoblique.png")) {
						Func<byte, byte, byte, byte, Color> shader = delegate(byte A, byte R, byte G, byte B) {
							return Color.FromArgb(A, _clamp((255 - R) * 0.8), _clamp((255 - G) * 0.6), 0);
						};
						//F68D00
						return _applyShader(img, shader);
					}

					return img;
				};

				if (ActEditorConfiguration.StyleTheme != "Dark theme") {
					var path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "Themes", ActEditorConfiguration.StyleTheme + ".xaml");

					try {
						Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.RelativeOrAbsolute) });
					}
					catch (Exception err) {
						ActEditorConfiguration.StyleTheme = "Dark theme";
						ErrorHandler.HandleException(err);
					}
				}
			}

			if (!Methods.IsWinVistaOrHigher() && Methods.IsWinXPOrHigher()) {
				// We are on Windows XP, force the style.
				try {
					Uri uri = new Uri("PresentationFramework.Aero;V3.0.0.0;31bf3856ad364e35;component\\themes/aero.normalcolor.xaml", UriKind.Relative);
					Resources.MergedDictionaries.Add(LoadComponent(uri) as ResourceDictionary);
				}
				catch {
					MessageBox.Show("Failed to apply a style override for Windows XP's theme.");
				}
			}

			base.OnStartup(e);
		}

		private byte _clamp(int val) {
			if (val < 0)
				return 0;
			if (val > 255)
				return 255;
			return (byte)val;
		}

		private byte _clamp(double val) {
			if (val < 0)
				return 0;
			if (val > 255)
				return 255;
			return (byte)val;
		}

		private void _darkTheme(byte[] pixels, PixelFormat format, List<Color> colors, Func<byte, byte, byte, byte, Color> shader) {
			if (format.BitsPerPixel / 8 == 1) {
				for (int i = 0; i < colors.Count; i++) {
					colors[i] = shader(colors[i].A, colors[i].R, colors[i].G, colors[i].B);
				}
			}
			else if (format.BitsPerPixel / 8 == 3) {
				for (int i = 0; i < pixels.Length; i += 3) {
					Color c = shader(255, pixels[i + 2], pixels[i + 1], pixels[i + 0]);

					pixels[i + 0] = c.B;
					pixels[i + 1] = c.G;
					pixels[i + 2] = c.R;
				}
			}
			else if (format.BitsPerPixel / 8 == 4) {
				for (int i = 0; i < pixels.Length; i += 4) {
					Color c = shader(pixels[i + 3], pixels[i + 2], pixels[i + 1], pixels[i + 0]);

					pixels[i + 0] = c.B;
					pixels[i + 1] = c.G;
					pixels[i + 2] = c.R;
					pixels[i + 3] = c.A;
				}
			}
		}

		private WriteableBitmap _applyShader(BitmapFrame img, Func<byte, byte, byte, byte, Color> shader) {
			const double DPI = 96;

			if (Methods.CanUseIndexed8 || img.Format != PixelFormats.Indexed8) {
				int width = img.PixelWidth;
				int height = img.PixelHeight;

				int stride = (int)Math.Ceiling(width * img.Format.BitsPerPixel / 8f);
				byte[] pixelData = new byte[stride * height];
				img.CopyPixels(pixelData, stride, 0);
				_darkTheme(pixelData, img.Format, null, shader);
				var wBitmap = new WriteableBitmap(BitmapSource.Create(width, height, DPI, DPI, img.Format, img.Palette, pixelData, stride));
				wBitmap.Freeze();
				return wBitmap;
			}
			else {
				List<Color> colors = new List<Color>(img.Palette.Colors);
				byte[] pixelData = new byte[img.PixelWidth * img.PixelHeight * img.Format.BitsPerPixel / 8];
				img.CopyPixels(pixelData, img.PixelWidth * img.Format.BitsPerPixel / 8, 0);
				_darkTheme(pixelData, img.Format, colors, shader);
				var wBitmap = WpfImaging.ToBgra32FromIndexed8(pixelData, colors, img.PixelWidth, img.PixelHeight);
				wBitmap.Freeze();
				return wBitmap;
			}
		}
	}
}