using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GrfToWpfBridge;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SoundEditDialog.xaml
	/// </summary>
	public partial class GifSavingDialog : TkWindow {
		private readonly List<string> _extra = new List<string>();

		public GifSavingDialog() {
			InitializeComponent();
		}

		public GifSavingDialog(Act act, int selectedIndex) : base("Gif saving", "app.ico") {
			InitializeComponent();

			if (act == null) {
				Loaded += delegate {
					ErrorHandler.HandleException("No Act loaded.");
					Close();
				};

				return;
			}

			_tbIndexFrom.Text = "0";
			_tbIndexTo.Text = act[selectedIndex].NumberOfFrames.ToString(CultureInfo.InvariantCulture);
			Binder.Bind(_cbUniform, () => ActEditorConfiguration.ActEditorGifUniform, v => ActEditorConfiguration.ActEditorGifUniform = v);
			Binder.Bind(_colorBackground, () => ActEditorConfiguration.ActEditorGifBackgroundColor, v => ActEditorConfiguration.ActEditorGifBackgroundColor = v);
			Binder.Bind(_colorGuildelines, () => ActEditorConfiguration.ActEditorGifGuidelinesColor, v => ActEditorConfiguration.ActEditorGifGuidelinesColor = v);
			Binder.Bind(_cbDoNotShow, () => ActEditorConfiguration.ActEditorGifHideDialog, v => ActEditorConfiguration.ActEditorGifHideDialog = v);

			int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;
			_tbDelay.Text = ((int)Math.Ceiling((act[selectedIndex].AnimationSpeed * frameInterval))).ToString(CultureInfo.InvariantCulture);

			Binder.Bind(_tbDelayFactor, () => ActEditorConfiguration.ActEditorGifDelayFactor, v => ActEditorConfiguration.ActEditorGifDelayFactor = v);
			Binder.Bind(_tbMargin, () => ActEditorConfiguration.ActEditorGifMargin, v => ActEditorConfiguration.ActEditorGifMargin = v);
		}

		public string[] Extra {
			get {
				_extra.Clear();

				_extra.Add("indexFrom");
				_extra.Add(_tbIndexFrom.Text);

				_extra.Add("indexTo");
				_extra.Add(_tbIndexTo.Text);

				_extra.Add("uniform");
				_extra.Add(ActEditorConfiguration.ActEditorGifUniform.ToString());

				_extra.Add("background");
				_extra.Add(ActEditorConfiguration.ActEditorGifBackgroundColor.ToGrfColor().ToHexString().Replace("0x", "#"));

				_extra.Add("guideLinesColor");
				_extra.Add(ActEditorConfiguration.ActEditorGifGuidelinesColor.ToGrfColor().ToHexString().Replace("0x", "#"));

				_extra.Add("scaling");
				_extra.Add(ActEditorConfiguration.ActEditorScalingMode.ToString());

				_extra.Add("delay");
				_extra.Add(_tbDelay.Text);

				_extra.Add("delayFactor");
				_extra.Add(_tbDelayFactor.Text);

				_extra.Add("margin");
				_extra.Add(ActEditorConfiguration.ActEditorGifMargin.ToString(CultureInfo.InvariantCulture));

				return _extra.ToArray();
			}
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape)
				Close();
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
			Close();
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}
	}
}