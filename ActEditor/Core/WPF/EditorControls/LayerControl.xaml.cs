using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.FrameEditor;
using ActEditor.Core.WPF.GenericControls;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GrfToWpfBridge;
using Utilities;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Reference LayerControl for displaying Layer's properties
	/// </summary>
	public partial class LayerControl : UserControl {
		private readonly FrameRenderer _renderer;
		public string ReferenceName { get; set; }
		private Act _act;
		private bool _eventsEnabled = true;

		public LayerControl() {
			InitializeComponent();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LayerControl" /> class.
		/// This constructure is only meant to be used for references.
		/// </summary>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="name"> </param>
		public LayerControl(TabAct actEditor, string name) {
			ReferenceName = name;
			_renderer = (FrameRenderer)actEditor.FrameRenderer;

			InitializeComponent();

			LoadSavedReferenceValues();
			_initReferenceEvents();
		}

		public void LoadSavedReferenceValues() {
			List<string> data = Methods.StringToList(ActEditorConfiguration.ConfigAsker["[ActEditor - " + ReferenceName + "]", "0,0,0,false,#FFFFFFFF,1,1,0"]);
			_eventsEnabled = false;

			try {
				_tbOffsetX.Text = data[1];
				_tbOffsetY.Text = data[2];
				_cbMirror.IsChecked = Boolean.Parse(data[3]);
				_color.Color = new GrfColor(data[4]).ToColor();
				_tbScaleX.Text = data[5];
				_tbScaleY.Text = data[6];
				_tbRotation.Text = data[7];
			}
			finally {
				_eventsEnabled = true;
			}
		}

		private void _initReferenceEvents() {
			RoutedEventHandler update = delegate {
				if (!_eventsEnabled) return;

				ApplyReferenceLayerValues();
			};

			_tbOffsetX.TextChanged += _tceh;
			_tbOffsetY.TextChanged += _tceh;
			_cbMirror.Checked += update;
			_cbMirror.Unchecked += update;
			_color.ColorChanged += (e, a) => update(null, null);
			_color.PreviewColorChanged += (e, a) => update(null, null);
			_tbScaleX.TextChanged += _tcehf;
			_tbScaleY.TextChanged += _tcehf;
			_tbRotation.TextChanged += _tceh;
		}

		private void _tceh(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			if (Int32.TryParse((sender as ClickSelectTextBox2).Text, out int ival)) {
				ApplyReferenceLayerValues();
			}
		}

		private void _tcehf(object sender, TextChangedEventArgs e) {
			if (!_eventsEnabled) return;

			if (_getDecimalVal(((ClickSelectTextBox2)sender).Text, out float dval)) {
				ApplyReferenceLayerValues();
			}
		}

		public void ApplyReferenceLayerValues() {
			try {
				if (_act == null) return;

				_act.Commands.UndoAll();

				_act.Commands.Translate(Int32.Parse(_tbOffsetX.Text), Int32.Parse(_tbOffsetY.Text));
				_act.Commands.Scale(_getDecimalVal(_tbScaleX.Text), _getDecimalVal(_tbScaleY.Text));
				_act.Commands.Rotate(Int32.Parse(_tbRotation.Text));

				if (_cbMirror.IsChecked == true)
					_act.Commands.SetMirror(_cbMirror.IsChecked == true);

				if (!_color.Color.ToGrfColor().Equals(GrfColors.White))
					_act.Commands.SetColor(_color.Color.ToGrfColor());

				List<string> data = new List<string> {
					"",
					_tbOffsetX.Text,
					_tbOffsetY.Text,
					_cbMirror.IsChecked.ToString(),
					_color.Color.ToGrfColor().ToHexString(),
					_tbScaleX.Text,
					_tbScaleY.Text,
					_tbRotation.Text
				};

				ActEditorConfiguration.ConfigAsker["[ActEditor - " + ReferenceName + "]"] = Methods.ListToString(data);
			}
			catch {
			}

			_renderer?.Update();
		}

		private static float _getDecimalVal(string text) {
			float dval;
			_getDecimalVal(text, out dval);
			return dval;
		}

		private static bool _getDecimalVal(string text, out float dval) {
			if (float.TryParse(text, out dval)) {
				return true;
			}

			text = text.Replace(",", ".");

			if (float.TryParse(text, out dval)) {
				return true;
			}

			text = text.Replace(".", ",");

			if (float.TryParse(text, out dval)) {
				return true;
			}

			return false;
		}

		public void ReferenceSetAndUpdate(Act act) {
			_act = act;
			ApplyReferenceLayerValues();
		}
	}
}