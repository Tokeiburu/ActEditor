using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ActEditor.Tools.PaletteEditorTool {
	public class CursorHelper {
		private static class NativeMethods {
			public struct IconInfo {
				public bool fIcon;
				public int xHotspot;
				public int yHotspot;
				public IntPtr hbmMask;
				public IntPtr hbmColor;
			}

			[DllImport("user32.dll")]
			public static extern SafeIconHandle CreateIconIndirect(ref IconInfo icon);

			[DllImport("user32.dll")]
			public static extern bool DestroyIcon(IntPtr hIcon);

			[DllImport("user32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);
		}

		[SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
		private class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid {
			public SafeIconHandle()
				: base(true) {
			}

			override protected bool ReleaseHandle() {
				return NativeMethods.DestroyIcon(handle);
			}
		}

		private static Cursor InternalCreateCursor(Bitmap bmp, Point hotspot) {
			var iconInfo = new NativeMethods.IconInfo();
			NativeMethods.GetIconInfo(bmp.GetHicon(), ref iconInfo);

			iconInfo.xHotspot = (int)hotspot.X;
			iconInfo.yHotspot = (int)hotspot.Y;
			iconInfo.fIcon = false;

			SafeIconHandle cursorHandle = NativeMethods.CreateIconIndirect(ref iconInfo);
			return CursorInteropHelper.Create(cursorHandle);
		}

		public static Cursor CreateCursor(UIElement element, Point hotspot) {
			element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			element.Arrange(new Rect(new Point(), element.DesiredSize));

			RenderTargetBitmap rtb =
			  new RenderTargetBitmap(
				(int)element.DesiredSize.Width,
				(int)element.DesiredSize.Height,
				96, 96, PixelFormats.Pbgra32);

			rtb.Render(element);

			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(rtb));

			using (var ms = new MemoryStream()) {
				encoder.Save(ms);
				using (var bmp = new Bitmap(ms)) {
					return InternalCreateCursor(bmp, hotspot);
				}
			}
		}
	}
}
