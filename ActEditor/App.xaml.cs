using System;
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
using GRF.GrfSystem;
using GrfToWpfBridge.Application;
using TokeiLibrary;
using Utilities;
using Utilities.Services;
using System.IO;

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
			Spr.EnableImageSizeCheck = false;
			Spr.AutomaticDowngradeOnRleException = true;
			Configuration.ProgramDataPath = GrfPath.Combine(Configuration.ApplicationDataPath, ActEditorConfiguration.ProgramName);
			EffectConfiguration.ConfigAsker = ActEditorConfiguration.ConfigAsker;
			EncodingService.SetDisplayEncoding(ActEditorConfiguration.EncodingCodepage);
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
			ApplicationManager.ThemeChanged += delegate {
				try {
					AppContext.SetSwitch("Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering", ActEditorConfiguration.ThemeIndex == 0);
				}
				catch {
				}
			};

			ApplicationManager.CrashReportEnabled = true;
			ImageConverterManager.AddConverter(new DefaultImageConverter());

			Configuration.SetImageRendering(Resources);

			Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/GRFEditorStyles.xaml", UriKind.RelativeOrAbsolute) });

			if (ActEditorConfiguration.StyleTheme == "") {
				ActEditorConfiguration.ThemeIndex = 0;
			}
			else {
				Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/StyleDark.xaml", UriKind.RelativeOrAbsolute) });

				ApplicationManager.ImageProcessing = delegate(string name, BitmapFrame img) {
					if (img == null) return null;

					var pName = Path.GetFileName(name).ToLowerInvariant();
					Func<byte, byte, byte, byte, Color> shader;

					switch (pName) {
						case "reset.png":
							shader = delegate(byte A, byte R, byte G, byte B) {
								return Color.FromArgb(A, Methods.ClampToColorByte((R) * 1.8), Methods.ClampToColorByte(G / 3), Methods.ClampToColorByte(B / 3));
							};

							return _applyShader(img, shader);
						case "eye.png":
						case "smallArrow.png":
						case "cs_pen.png":
						case "cs_eraser.png":
						case "cs_line.png":
						case "cs_circle.png":
						case "cs_eraser2.png":
						case "cs_bucket.png":
							shader = delegate(byte A, byte R, byte G, byte B) {
								return Color.FromArgb(A, Methods.ClampToColorByte((255 - R) * 0.8), Methods.ClampToColorByte((255 - G) * 0.8), Methods.ClampToColorByte((255 - B) * 0.8));
							};

							return _applyShader(img, shader);
						case "arrow.png":
						case "arrowoblique.png":
							shader = delegate (byte A, byte R, byte G, byte B) {
								return Color.FromArgb(A, Methods.ClampToColorByte((255 - R) * 0.8), Methods.ClampToColorByte((255 - G) * 0.6), 0);
							};

							return _applyShader(img, shader);
					}

					return img;
				};

				ActEditorConfiguration.ThemeIndex = 1;

				try {
					AppContext.SetSwitch("Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering", false);
				}
				catch {
				}

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