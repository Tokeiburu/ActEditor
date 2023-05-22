using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ActEditor.Core.Avalon;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using TokeiLibrary.WPF.Styles;
using Utilities;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SoundEditDialog.xaml
	/// </summary>
	public partial class SoundEditDialog : TkWindow {
		private readonly Act _act;

		public SoundEditDialog() {
			InitializeComponent();
		}

		public SoundEditDialog(Act act) : base("Sound edit", "app.ico") {
			InitializeComponent();
			_act = act;
			AvalonLoader.Load(_textEditor);
			_textEditor.Text = string.Join("\r\n", _act.SoundFiles.ToArray());
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape)
				Close();
		}

		protected override void OnClosing(CancelEventArgs e) {
			if (DialogResult == true) {
				List<string> soundFiles = _textEditor.Text.Split(new string[] {"\r\n"}, StringSplitOptions.None).ToList();
				while (soundFiles.Count > 0 && soundFiles.Last() == "") {
					soundFiles.RemoveAt(soundFiles.Count - 1);
				}

				for (int i = 0; i < soundFiles.Count; i++) {
					if (soundFiles[i].Length > 39) {
						ErrorHandler.HandleException("The sound file at " + (i + 1) + " has a name too long. It must be below 40 characters.");
						DialogResult = false;
						e.Cancel = true;
						return;
					}
				}

				if (soundFiles.Count == _act.SoundFiles.Count) {
					if (Methods.ListToString(soundFiles) == Methods.ListToString(_act.SoundFiles))
						return;
				}

				try {
					_act.Commands.Begin();
					_act.Commands.Backup(act => {
						act.SoundFiles.Clear();
						act.SoundFiles.AddRange(soundFiles);

						foreach (Frame frame in act.GetAllFrames()) {
							if (frame.SoundId >= soundFiles.Count) {
								frame.SoundId = -1;
							}
						}
					}, "Sound list modified");
				}
				catch (Exception err) {
					_act.Commands.CancelEdit();
					ErrorHandler.HandleException(err);
				}
				finally {
					_act.Commands.End();
					_act.InvalidateVisual();
				}
			}

			base.OnClosing(e);
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