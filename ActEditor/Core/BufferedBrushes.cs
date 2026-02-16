using System;
using System.Collections.Generic;
using System.Windows.Media;
using GRF.Image;
using GrfToWpfBridge;
using static ActEditor.ApplicationConfiguration.ActEditorConfiguration;

namespace ActEditor.Core {
	/// <summary>
	/// Store brushes in a dictionary (and freeze them) to reuse later. 
	/// This speeds up the rendering time.
	/// </summary>
	public static class BufferedBrushes {
		private static readonly Dictionary<string, Func<GrfColor>> _getters = new Dictionary<string, Func<GrfColor>>();
		private static readonly Dictionary<string, GrfColor> _current = new Dictionary<string, GrfColor>();
		private static readonly Dictionary<string, Brush> _brushes = new Dictionary<string, Brush>();

		/// <summary>
		/// Gets the buffered brush.
		/// </summary>
		/// <param name="brushName">Name of the brush.</param>
		/// <returns></returns>
		public static Brush GetBrush(string brushName) {
			GrfColor activeColor = _getters[brushName]();
			GrfColor current = _current[brushName];

			if (current == null || !activeColor.Equals(current)) {
				_current[brushName] = activeColor;
				var brush = new SolidColorBrush(activeColor.ToColor());
				_brushes[brushName] = brush;
				brush.Freeze();
				return brush;
			}

			return _brushes[brushName];
		}

		/// <summary>
		/// Registers the specified brush name and freeze the brush for future use. If the color changes, the brush will be remade.
		/// </summary>
		/// <param name="brushName">Name of the brush.</param>
		/// <param name="getter">The function to retrieve the current color of the registered brush.</param>
		public static void Register(string brushName, Func<GrfColor> getter) {
			if (_current.ContainsKey(brushName)) return;

			GrfColor activeColor = getter();
			_getters[brushName] = getter;
			_current[brushName] = getter();
			var brush = new SolidColorBrush(activeColor.ToColor());
			brush.Freeze();
			_brushes[brushName] = brush;
		}

		public static void Register(string brushName, QuickSetting<GrfColor> setting) {
			if (_current.ContainsKey(brushName)) return;

			GrfColor activeColor = setting.Get();
			_getters[brushName] = setting.Get;
			_current[brushName] = activeColor;
			var brush = new SolidColorBrush(activeColor.ToColor());
			brush.Freeze();
			_brushes[brushName] = brush;
		}
	}
}