using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using GRF.Image;
using TokeiLibrary;
using Utilities.Extension;

namespace ActEditor.ApplicationConfiguration {
	/// <summary>
	/// Class imported from GrfEditor
	/// </summary>
	public class GrfEditorIconProvider {
		private static readonly Dictionary<string, BitmapSource> _associatedExtensions = new Dictionary<string, BitmapSource>();
		private static readonly Dictionary<string, BitmapSource> _associatedLargeExtensions = new Dictionary<string, BitmapSource>();

		public static BitmapSource GetLargeIcon(string file) {
			string extension = file.GetExtension();

			if (extension == null)
				return null;

			if (!_associatedLargeExtensions.ContainsKey(extension)) {
				_loadLargeExtension(extension);
			}

			return _associatedLargeExtensions[extension];
		}

		public static BitmapSource GetIcon(string file) {
			string extension = file.GetExtension();

			if (extension == null)
				return null;

			if (!_associatedExtensions.ContainsKey(extension)) {
				_loadExtension(extension);
			}

			return _associatedExtensions[extension];
		}

		private static void _loadExtension(string extension) {
			byte[] buffer;
			GrfImage image;

			try {
				switch (extension) {
					case ".pal":
						buffer = ApplicationManager.GetResource("pal.png");
						image = new GrfImage(ref buffer);
						_associatedExtensions.Add(extension, image.Cast<BitmapSource>());
						break;
					case ".gnd":
					case ".rsw":
					case ".gat":
					case ".rsm":
					case ".lua":
					case ".lub":
					case ".imf":
					case ".xml":
					case ".str":
						buffer = ApplicationManager.GetResource("file_" + extension.Substring(1) + ".png");

						if (buffer == null) {
							_loadAny(extension);
						}
						else {
							image = new GrfImage(ref buffer);
							_associatedExtensions.Add(extension, image.Cast<BitmapSource>());
						}
						break;
					default:
						_loadAny(extension);
						break;
				}
			}
			catch {
				buffer = ApplicationManager.GetResource("pal.png");
				image = new GrfImage(ref buffer);
				_associatedExtensions.Add(extension, image.Cast<BitmapSource>());
			}
		}

		private static void _loadAny(string extension) {
			Icon temp = IconReader.GetFileIcon(extension, true, false);
			Bitmap bitmap = temp.ToBitmap();
			byte[] buffer;

			using (MemoryStream stream = new MemoryStream()) {
				bitmap.Save(stream, ImageFormat.Png);

				buffer = stream.GetBuffer();
			}

			GrfImage image = new GrfImage(ref buffer);
			_associatedExtensions.Add(extension, image.Cast<BitmapSource>());
		}

		private static void _loadLargeExtension(string extension) {
			byte[] buffer;
			GrfImage image;

			try {
				switch (extension) {
					default:
						Icon temp = IconReader.GetFileIcon(extension, false, false);
						Bitmap bitmap = temp.ToBitmap();

						using (MemoryStream stream = new MemoryStream()) {
							bitmap.Save(stream, ImageFormat.Png);

							buffer = stream.GetBuffer();
						}

						image = new GrfImage(ref buffer);
						_associatedLargeExtensions.Add(extension, image.Cast<BitmapSource>());
						break;
				}
			}
			catch {
				buffer = ApplicationManager.GetResource("pal.png");
				image = new GrfImage(ref buffer);
				_associatedLargeExtensions.Add(extension, image.Cast<BitmapSource>());
			}
		}
	}
}