using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.EditorControls;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ActionInsertDialog.xaml
	/// </summary>
	public partial class FrameInsertDialog : TkWindow {
		#region EditMode enum

		public enum EditMode {
			None,
			Delete,
			Insert,
			Move,
			Switch,
			Replace,
		}

		#endregion

		private readonly Act _act;

		public FrameInsertDialog() : base("Frame edit", "advanced.png") {
			InitializeComponent();

			WpfUtilities.AddFocus(_tbIndexStart, _tbIndexEnd, _tbIndexRange);

			_asIndexStart.FrameChanged += new ActIndexSelector.FrameIndexChangedDelegate(_asIndexStart_ActionChanged);
			_asIndexEnd.FrameChanged += new ActIndexSelector.FrameIndexChangedDelegate(_asIndexEnd_ActionChanged);

			_tbIndexEnd.TextChanged += delegate {
				int ival;

				if (Int32.TryParse(_tbIndexEnd.Text, out ival)) {
					_asIndexEnd.SelectedFrame = ival;
				}
			};

			_tbIndexStart.TextChanged += delegate {
				int ival;

				if (Int32.TryParse(_tbIndexStart.Text, out ival)) {
					_asIndexStart.SelectedFrame = ival;
				}
			};

			Binder.Bind(_cbCopyContent, () => ActEditorConfiguration.ActEditorCopyFromCurrentFrame, v => ActEditorConfiguration.ActEditorCopyFromCurrentFrame = v, delegate {
				if (ActEditorConfiguration.ActEditorCopyFromCurrentFrame) {
					_labelStartIndex.Visibility = Visibility.Visible;
					_borderIndexStart.Visibility = Visibility.Visible;
					_asIndexStart.Visibility = Visibility.Visible;
				}
				else {
					_labelStartIndex.Visibility = Visibility.Collapsed;
					_borderIndexStart.Visibility = Visibility.Collapsed;
					_asIndexStart.Visibility = Visibility.Collapsed;
				}
			});
		}

		public FrameInsertDialog(Act act, int actionIndex)
			: this() {
			ActionIndex = actionIndex;
			_asIndexStart.Set(act, actionIndex);
			_asIndexEnd.Set(act, actionIndex);
			_act = act;
		}

		public int StartIndex {
			get {
				int index;

				if (Int32.TryParse(_tbIndexStart.Text, out index)) {
					return index;
				}

				return -1;
			}
			set { _tbIndexStart.Text = value.ToString(CultureInfo.InvariantCulture); }
		}

		public int EndIndex {
			get {
				int index;

				if (Int32.TryParse(_tbIndexEnd.Text, out index)) {
					return index;
				}

				return -1;
			}
			set {
				_tbIndexEnd.Text = value.ToString(CultureInfo.InvariantCulture);

				if (Int32.Parse(_tbIndexEnd.Text) != value) {
					_tbIndexEnd.Text = value.ToString(CultureInfo.InvariantCulture);
				}
			}
		}

		public int Range {
			get {
				int index;

				if (Int32.TryParse(_tbIndexRange.Text, out index)) {
					return index;
				}

				return -1;
			}
			set { _tbIndexRange.Text = value.ToString(CultureInfo.InvariantCulture); }
		}

		public EditMode Mode {
			get {
				return
					_mode0.IsChecked == true ? EditMode.Delete :
						                                           _mode1.IsChecked == true ? EditMode.Insert :
							                                                                                      _mode2.IsChecked == true ? EditMode.Move :
								                                                                                                                               _mode3.IsChecked == true ? EditMode.Switch :
									                                                                                                                                                                          _mode4.IsChecked == true ? EditMode.Replace :
										                                                                                                                                                                                                                      EditMode.None;
			}
			set {
				switch (value) {
					case EditMode.Delete:
						_mode0.IsChecked = true;
						_deleteMode();
						break;
					case EditMode.Insert:
						_mode1.IsChecked = true;
						_insertMode();
						break;
					case EditMode.Move:
						_mode2.IsChecked = true;
						_setAllVisible();

						_cbCopyContent.Visibility = Visibility.Collapsed;
						break;
					case EditMode.Switch:
						_mode3.IsChecked = true;
						_setAllVisible();

						_cbCopyContent.Visibility = Visibility.Collapsed;
						break;
					case EditMode.Replace:
						_mode4.IsChecked = true;
						_setAllVisible();

						_cbCopyContent.Visibility = Visibility.Collapsed;
						break;
				}
			}
		}

		public int ActionIndex { get; set; }

		private void _insertMode() {
			_setAllVisible();

			if (!ActEditorConfiguration.ActEditorCopyFromCurrentFrame) {
				_labelStartIndex.Visibility = Visibility.Collapsed;
				_borderIndexStart.Visibility = Visibility.Collapsed;
				_asIndexStart.Visibility = Visibility.Collapsed;
			}
		}

		private void _deleteMode() {
			_setAllVisible();

			_cbCopyContent.Visibility = Visibility.Collapsed;
			_gridEndIndex.Visibility = Visibility.Collapsed;
			_borderIndexEnd.Visibility = Visibility.Collapsed;
			_asIndexEnd.Visibility = Visibility.Collapsed;
		}

		private void _setAllVisible() {
			_labelStartIndex.Visibility = Visibility.Visible;
			_labelRange.Visibility = Visibility.Visible;
			_gridEndIndex.Visibility = Visibility.Visible;
			_cbCopyContent.Visibility = Visibility.Visible;
			_borderIndexStart.Visibility = Visibility.Visible;
			_borderRange.Visibility = Visibility.Visible;
			_borderIndexEnd.Visibility = Visibility.Visible;
			_asIndexStart.Visibility = Visibility.Visible;
			_asIndexEnd.Visibility = Visibility.Visible;
		}

		private void _asIndexEnd_ActionChanged(object sender, int actionindex) {
			_tbIndexEnd.Text = actionindex.ToString(CultureInfo.InvariantCulture);
		}

		private void _asIndexStart_ActionChanged(object sender, int actionindex) {
			_tbIndexStart.Text = actionindex.ToString(CultureInfo.InvariantCulture);
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				DialogResult = true;
			}
			base.GRFEditorWindowKeyDown(sender, e);
		}

		protected override void OnClosing(CancelEventArgs e) {
			if (DialogResult == true) {
				if (!CanExecute()) {
					DialogResult = null;
					e.Cancel = true;
				}
			}
		}

		public bool CanExecute() {
			if (Mode == EditMode.None) return false;
			try {
				Execute(_act, false);
				return true;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				return false;
			}
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		public void Execute(Act act, bool executeCommands = true) {
			switch (Mode) {
				case EditMode.Delete:
					if (StartIndex < 0) {
						StartIndex = 0;
					}

					if (StartIndex >= act[ActionIndex].NumberOfFrames) {
						StartIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (StartIndex + Range > act[ActionIndex].NumberOfFrames) {
						Range = act[ActionIndex].NumberOfFrames - StartIndex;
					}

					if (Range < 1) {
						Range = 1;
					}

					if (act[ActionIndex].NumberOfFrames - Range <= 0) {
						throw new ArgumentException("There must be at least one frame left in the act.");
					}

					if (executeCommands) {
						try {
							act.Commands.FrameDeleteRange(ActionIndex, StartIndex, Range);
						}
						catch (Exception err) {
							ErrorHandler.HandleException(err, ErrorLevel.Warning);
						}
						finally {
							act.InvalidateVisual();
						}
					}
					break;
				case EditMode.Insert:
					if (ActEditorConfiguration.ActEditorCopyFromCurrentFrame) {
						if (StartIndex < 0) {
							throw new ArgumentException("Start index must be greater or equal than 0.");
						}

						if (StartIndex >= act[ActionIndex].NumberOfFrames) {
							throw new ArgumentException("Start index must be less than the number of actions (" + act[ActionIndex].NumberOfFrames + ").");
						}
					}

					if (Range < 1) {
						Range = 1;
					}

					if (EndIndex > act[ActionIndex].NumberOfFrames) {
						EndIndex = act[ActionIndex].NumberOfFrames;
					}

					if (EndIndex < 0) {
						EndIndex = 0;
					}

					if (executeCommands) {
						try {
							act.Commands.BeginNoDelay();

							if (ActEditorConfiguration.ActEditorCopyFromCurrentFrame) {
								for (int i = 0; i < Range; i++) {
									if (EndIndex > StartIndex) {
										act.Commands.FrameCopyTo(ActionIndex, StartIndex, EndIndex);
									}
									else {
										act.Commands.FrameCopyTo(ActionIndex, StartIndex + i, EndIndex);
									}
								}
							}
							else {
								for (int i = 0; i < Range; i++) {
									act.Commands.FrameInsertAt(ActionIndex, EndIndex);
								}
							}
						}
						catch (Exception err) {
							act.Commands.CancelEdit();
							ErrorHandler.HandleException(err);
						}
						finally {
							act.Commands.End();
							act.InvalidateVisual();
						}
					}
					break;
				case EditMode.Move:
					if (StartIndex >= act[ActionIndex].NumberOfFrames) {
						StartIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (EndIndex >= act[ActionIndex].NumberOfFrames) {
						EndIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (StartIndex < 0) {
						StartIndex = 0;
					}

					if (EndIndex < 0) {
						EndIndex = 0;
					}

					if (StartIndex == EndIndex) {
						throw new Exception("The start and end indexes cannot be the same.");
					}

					if (Range < 1) {
						Range = 1;
					}

					if (StartIndex + Range > act[ActionIndex].NumberOfFrames) {
						Range = act[ActionIndex].NumberOfFrames - StartIndex;
						throw new Exception("The range goes beyond the number of actions.");
					}

					if (EndIndex <= StartIndex + Range && EndIndex + 1 > StartIndex) {
						throw new Exception("Indexes intersect with each other.");
					}

					if (executeCommands) {
						try {
							act.Commands.FrameMoveRange(ActionIndex, StartIndex, Range, EndIndex);
							//act.Commands.ActionMoveRange(StartIndex, Range, EndIndex);
						}
						catch (Exception err) {
							ErrorHandler.HandleException(err);
						}
						finally {
							act.InvalidateVisual();
						}
					}
					break;
				case EditMode.Switch:
					if (StartIndex >= act[ActionIndex].NumberOfFrames) {
						StartIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (EndIndex >= act[ActionIndex].NumberOfFrames) {
						EndIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (StartIndex < 0) {
						StartIndex = 0;
					}

					if (EndIndex < 0) {
						EndIndex = 0;
					}

					if (StartIndex == EndIndex) {
						throw new Exception("The start and end indexes cannot be the same.");
					}

					if (Range < 1) {
						Range = 1;
					}

					if (StartIndex + Range > act[ActionIndex].NumberOfFrames) {
						Range = act[ActionIndex].NumberOfFrames - StartIndex;
						throw new Exception("The range goes beyond the number of actions.");
					}

					if (EndIndex < StartIndex + Range && EndIndex + 1 > StartIndex) {
						throw new Exception("Indexes intersect with each other (overlapping).");
					}

					if (executeCommands) {
						try {
							act.Commands.FrameSwitchRange(ActionIndex, StartIndex, Range, EndIndex);
							//act.Commands.ActionSwitchRange(StartIndex, Range, EndIndex);
						}
						catch (Exception err) {
							ErrorHandler.HandleException(err);
						}
						finally {
							act.InvalidateVisual();
						}
					}
					break;
				case EditMode.Replace:
					if (StartIndex == EndIndex) {
						EndIndex = StartIndex + 1;
						Range--;
					}

					if (StartIndex >= act[ActionIndex].NumberOfFrames) {
						StartIndex = act[ActionIndex].NumberOfFrames - 1;
					}

					if (StartIndex < 0) {
						StartIndex = 0;
					}

					if (EndIndex < 0) {
						EndIndex = 0;
					}

					if (Range < 1) {
						Range = 1;
					}

					if (EndIndex < StartIndex + 1 && EndIndex + Range > StartIndex) {
						throw new Exception("Indexes intersect with each other (overlapping).");
					}

					if (executeCommands) {
						try {
							act.Commands.BeginNoDelay();

							while (EndIndex + Range > act[ActionIndex].NumberOfFrames) {
								act.Commands.FrameInsertAt(ActionIndex, act[ActionIndex].NumberOfFrames);
								//act.Commands.ActionInsertAt(act[ActionIndex].NumberOfFrames);
							}

							for (int i = 0; i < Range; i++) {
								act.Commands.FrameReplace(ActionIndex, StartIndex, EndIndex + i);
								//act.Commands.ActionReplaceTo(StartIndex, EndIndex + i);
							}
						}
						catch (Exception err) {
							ErrorHandler.HandleException(err);
						}
						finally {
							act.Commands.End();
							act.InvalidateVisual();
						}
					}
					break;
				case EditMode.None:
					throw new Exception("No command selected.");
			}
		}

		private void _mode_Checked(object sender, RoutedEventArgs e) {
			EditMode mode = EditMode.None;

			if (sender == _mode0)
				mode = EditMode.Delete;
			else if (sender == _mode1)
				mode = EditMode.Insert;
			else if (sender == _mode2)
				mode = EditMode.Move;
			else if (sender == _mode3)
				mode = EditMode.Switch;
			else if (sender == _mode4)
				mode = EditMode.Replace;

			Mode = mode;
		}

		private void _lastIndex_Click(object sender, RoutedEventArgs e) {
			EndIndex = _act[ActionIndex].NumberOfFrames;
		}
	}
}