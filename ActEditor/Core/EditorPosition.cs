using ActEditor.ApplicationConfiguration;
using System.Collections.Generic;
using System.Windows;
using Utilities;

namespace ActEditor.Core {
	public class EditorPosition {
		public bool IsSet { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public double X { get; set; }
		public double Y { get; set; }

		public void FromString(string config) {
			var items = Methods.StringToList(config, ';');
			int i = 0;

			if (items.Count == 0)
				return;

			IsSet = FormatConverters.BooleanConverter(items[i++]);
			Width = FormatConverters.DoubleConverterNoThrow(items[i++]);
			Height = FormatConverters.DoubleConverterNoThrow(items[i++]);
			X = FormatConverters.DoubleConverterNoThrow(items[i++]);
			Y = FormatConverters.DoubleConverterNoThrow(items[i++]);
		}

		public override string ToString() {
			if (!IsSet)
				return "";

			List<string> values = new List<string>();
			values.Add(IsSet + "");
			values.Add(Width + "");
			values.Add(Height + "");
			values.Add(X + "");
			values.Add(Y + "");

			return Methods.Aggregate(values, ";");
		}

		public void Load(Window editor) {
			FromString(ActEditorConfiguration.EditorSavedPositions);

			if (!IsSet || !ActEditorConfiguration.SaveEditorPosition)
				return;

			editor.Width = Width;
			editor.Height = Height;
			editor.Left = X;
			editor.Top = Y;
		}

		public void Save(Window editor) {
			if (editor.WindowState == WindowState.Maximized)
				return;

			IsSet = true;
			Width = editor.Width;
			Height = editor.Height;
			X = editor.Left;
			Y = editor.Top;
			Save();
		}

		public void Save() {
			ActEditorConfiguration.EditorSavedPositions = ToString();
		}
	}
}
