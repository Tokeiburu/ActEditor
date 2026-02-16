using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Utilities;

namespace ActEditor.Core.WPF.FrameEditor {
	public class BitmapResourceManager {
		public class BitmapHandle {
			public WriteableBitmap Bitmap;
			public bool InUse;
			public bool New;
			public DateTime CreationTime;
			public DateTime LastUse;
		}

		public class CachedHandle {
			public int GrfImageHash;
			public int Width;
			public int Height;
			public Dictionary<uint, BitmapHandle> Bitmaps = new Dictionary<uint, BitmapHandle>();
		}

		private List<CachedHandle> _cachedBitmaps = new List<CachedHandle>();
		//private Dictionary<(int w, int h, uint color, GrfImageType fmt), Stack<BitmapHandle>> _bitmapPool = new Dictionary<(int w, int h, uint color, GrfImageType fmt), Stack<BitmapHandle>>();

		public BitmapHandle GetBitmapHandle(SpriteIndex index, Act act, GrfImage image, GrfColor color) {
			Z.Start(101);
			ValidateCache(act);

			var handle = _getBitmapHandle(index, act, image, color);

			//if (image.GrfImageType == GrfImageType.Indexed8) {
			//	handle.Bitmap.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Pixels, image.Width, 0);
			//}
			//else {
			//	if (handle.New) {
			//		handle.Bitmap.Lock();
			//
			//		unsafe {
			//			fixed (byte* src = image.Pixels) {
			//				Buffer.MemoryCopy(
			//					src,
			//					(void*)handle.Bitmap.BackBuffer,
			//					image.Pixels.Length,
			//					image.Pixels.Length);
			//			}
			//		}
			//
			//		handle.Bitmap.AddDirtyRect(new Int32Rect(0, 0, image.Width, image.Height));
			//		handle.Bitmap.Unlock();
			//	}
			//}
			Z.Stop(101);

			return handle;
		}

		public void ClearCache() {
			foreach (var cacheBitmap in _cachedBitmaps) {
				cacheBitmap.Bitmaps.Clear();
			}

			_cachedBitmaps.Clear();
		}

		public void ValidateCache(Act act) {
			// Add missing cache
			while (_cachedBitmaps.Count < act.Sprite.NumberOfImagesLoaded) {
				_cachedBitmaps.Add(new CachedHandle());
			}

			// Remove extra cached images
			var max = act.Sprite.NumberOfImagesLoaded;

			for (int i = max; i < _cachedBitmaps.Count; i++) {
				_cachedBitmaps.RemoveAt(max);
			}

			// Ensure the cache has valid entries
			var images = act.Sprite.Images;
			var now = DateTime.Now;

			for (int i = 0; i < _cachedBitmaps.Count && i < max; i++) {
				var cacheBitmap = _cachedBitmaps[i];
				var image = images[i];

				{
					List<uint> toDeleteKeys = new List<uint>();

					foreach (var bitmapHandle in cacheBitmap.Bitmaps) {
						var elapsed = now - bitmapHandle.Value.LastUse;
						
						if (elapsed.TotalSeconds > 10) {
							toDeleteKeys.Add(bitmapHandle.Key);
						}
					}

					foreach (var key in toDeleteKeys) {
						cacheBitmap.Bitmaps.Remove(key);
					}
				}

				if (cacheBitmap.GrfImageHash == image.GetHashCode() &&
					cacheBitmap.Width == image.Width &&
					cacheBitmap.Height == image.Height) {
					continue;
				}

				cacheBitmap.GrfImageHash = image.GetHashCode();
				cacheBitmap.Width = image.Width;
				cacheBitmap.Height = image.Height;
				cacheBitmap.Bitmaps.Clear();
			}
		}

		private BitmapHandle _getBitmapHandle(SpriteIndex index, Act act, GrfImage image, GrfColor color) {
			int absoluteIndex = index.GetAbsoluteIndex(act.Sprite);
			uint colorKey = color.ToArgbInt32();

			CachedHandle cache = _cachedBitmaps[absoluteIndex];

			if (cache.Bitmaps.TryGetValue(colorKey, out BitmapHandle handle)) {
				handle.LastUse = DateTime.Now;
				return handle;
			}

			int width = image.Width;
			int height = image.Height;
			GrfImageType type = image.GrfImageType;
			BitmapPalette palette = null;

			handle = new BitmapHandle();
			cache.Bitmaps[colorKey] = handle;

			if (image.GrfImageType == GrfImageType.Indexed8) {
				palette = new BitmapPalette(_loadColors(image.Palette, color));
			}
			else {
				image = image.Copy();
				image.ApplyChannelColor(color);
			}

			int srcStride = width;
			int dstStride = ((width * 8 + 31) & ~31) / 8;

			handle.Bitmap = new WriteableBitmap(width, height, 96, 96, type == GrfImageType.Indexed8 ? PixelFormats.Indexed8 : PixelFormats.Bgra32, palette);

			try {
				handle.Bitmap.Lock();

				unsafe {
					fixed (byte* src = image.Pixels) {
						if (image.GrfImageType == GrfImageType.Indexed8 && srcStride != dstStride) {
							byte* pBackBuffer = (byte*)handle.Bitmap.BackBuffer;

							for (int y = 0; y < height; y++) {
								Buffer.MemoryCopy(
									src + (y * srcStride),
									pBackBuffer + (y * dstStride),
									srcStride,
									srcStride);
							}
						}
						else {
							Buffer.MemoryCopy(
								src,
								(void*)handle.Bitmap.BackBuffer,
								image.Pixels.Length,
								image.Pixels.Length);
						}
					}
				}

				handle.Bitmap.AddDirtyRect(new Int32Rect(0, 0, image.Width, image.Height));
			}
			finally {
				handle.Bitmap.Unlock();
			}

			handle.LastUse = DateTime.Now;
			handle.CreationTime = DateTime.Now;
			return handle;
			//if (_bitmapPool.Count > 50) {
			//	_bitmapPool.Clear();
			//}
			//
			//if (!_bitmapPool.TryGetValue((width, height, color, type), out Stack<BitmapHandle> handleStack)) {
			//	handleStack = new Stack<BitmapHandle>();
			//	_bitmapPool[(width, height, color, type)] = handleStack;
			//}
			//
			//foreach (var handle in handleStack) {
			//	handle.InUse = true;
			//	handle.New = false;
			//	return handle;
			//	//if (!handle.InUse) {
			//	//	handle.InUse = true;
			//	//	return handle;
			//	//}
			//}
			//
			//BitmapHandle nHandle = new BitmapHandle();
			//BitmapPalette palette = null;
			//if (type == GrfImageType.Indexed8)
			//	palette = new BitmapPalette(_loadColors(image.Palette));
			//
			//nHandle.Bitmap = new WriteableBitmap(width, height, 96, 96, type == GrfImageType.Indexed8 ? PixelFormats.Indexed8 : PixelFormats.Bgra32, palette);
			//nHandle.InUse = true;
			//nHandle.New = true;
			//handleStack.Push(nHandle);
			//return nHandle;
		}

		private List<Color> _loadColors(byte[] palette, GrfColor multColor) {
			if (palette == null)
				throw new Exception("Palette not loaded.");

			List<Color> colors = new List<Color>(256);

			for (int i = 0, count = palette.Length; i < count; i += 4) {
				colors.Add(Color.FromArgb(
					(byte)(palette[i + 3] * multColor.A / 255), 
					(byte)(palette[i + 0] * multColor.R / 255), 
					(byte)(palette[i + 1] * multColor.G / 255), 
					(byte)(palette[i + 2] * multColor.B / 255)));
			}

			return colors;
		}

		internal void PrintDebug() {
			//foreach (var group in _cachedBitmaps) {
			//	Console.WriteLine("group count: " + group.Value.Count);
			//
			//	foreach (var st in group.Value) {
			//		Console.WriteLine("\t: " + st.GetHashCode() + " - " + st.InUse);
			//	}
			//}
		}
	}
}
