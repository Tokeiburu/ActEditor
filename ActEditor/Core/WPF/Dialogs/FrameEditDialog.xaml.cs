using ActEditor.Core.ListEditCommands;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.FrameEditor;
using GRF.FileFormats.ActFormat;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ActionEditDialog.xaml
	/// </summary>
	public partial class FrameEditDialog : TkWindow {
		private Act _originalAct;
		private GRF.FileFormats.ActFormat.Action _originalAction;
		private Act _act;
		private int _selectedAction;
		private RangeObservableCollection<IEditData> _actions = new RangeObservableCollection<IEditData>();
		private DummyFrameEditor _editor;
		private CommandsHolder _commands;
		private SelectableListBoxExtension _listBoxExtension;
		private ActEditListInteraction _editListInteraction;
		public RangeObservableCollection<IEditData> Actions => _actions;

		public FrameEditDialog() : base("Frame edit", "advanced.png") {
			InitializeComponent();

			_commands = new CommandsHolder(_actions);
			_listBoxExtension = new SelectableListBoxExtension(this, _lvActions, _gridEvents, _rectSelection, _rectInsertion, 100, 18);
			_editListInteraction = new ActEditListInteraction(_commands, this, _lvActions, _listBoxExtension);
		}

		public FrameEditDialog(Act act, int actionIndex, int frameIndex) : this() {
			_originalAct = act;
			_originalAction = act[actionIndex];
			_act = new Act(act);
			_selectedAction = actionIndex;

			InitializeFrames();
			InitializeEditor(actionIndex, frameIndex);
			InitializeShortcuts();

			DataContext = this;

			_lvActions.SelectionChanged += _lvActions_SelectionChanged;
			_commands.ModifiedStateChanged += _commands_ModifiedStateChanged;

			if (frameIndex >= 0 && frameIndex < _actions.Count) {
				_lvActions.SelectedItem = _actions[frameIndex];

				Loaded += delegate {
					_lvActions.ScrollToCenterOfView(_actions[frameIndex]);
				};
			}
		}

		public void InitializeShortcuts() {
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-N", "ListData.New"), () => _editListInteraction.New(new FrameIndexData()), this);
		}

		private void _lvActions_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_lvActions.SelectedItem != null) {
				_actIndexSelector.SelectedFrame = ((IEditData)_lvActions.SelectedItem).Index;
			}
		}

		public void InitializeEditor(int actionIndex, int frameIndex) {
			DummyFrameEditor editor = new DummyFrameEditor();
			editor.ActFunc = () => _act;
			editor.Element = this;
			editor.IndexSelector = _actIndexSelector;
			editor.SelectedActionFunc = () => _actIndexSelector.SelectedAction;
			editor.SelectedFrameFunc = () => _actIndexSelector.SelectedFrame;
			editor.FrameRenderer = _rfp;
			_editor = editor;

			_actIndexSelector.Init(_editor, _actIndexSelector.SelectedAction, _actIndexSelector.SelectedFrame);
			_actIndexSelector.SelectedAction = actionIndex;
			_actIndexSelector.SelectedFrame = frameIndex;
			_actIndexSelector.DisableActionChange();

			_rfp.DrawingModules.Add(new DefaultDrawModule(delegate {
				if (editor.Act != null) {
					return new List<DrawingComponent> { new ActDraw(editor.Act, editor) };
				}

				return new List<DrawingComponent>();
			}, DrawingPriorityValues.Normal, false));

			_rfp.Init(editor);
			_rfp.Update();

			Owner = ActEditorWindow.Instance;

			Closing += delegate {
				ActEditorWindow.Instance.Focus();
			};
		}

		private void _commands_ModifiedStateChanged(object sender, IListEditCommand<IEditData> command) {
			// Update the data list!
			_actions.Disable();

			for (int i = 0; i < _actions.Count; i++) {
				_actions[i].Index = i;
			}

			_actions.UpdateAndEnable();

			// Update editor preview
			var act = new Act(_act);
			var action = act.Actions[_selectedAction];
			action.Frames.Clear();

			for (int i = 0; i < _actions.Count; i++) {
				var index = ((FrameIndexData)_actions[i]).OriginalIndex;

				if (index < 0)
					action.Frames.Add(new GRF.FileFormats.ActFormat.Frame());
				else
					action.Frames.Add(new GRF.FileFormats.ActFormat.Frame(_originalAction[index]));
			}

			_act = act;

			var oldSelectedFrame = _actIndexSelector.SelectedFrame;
			_actIndexSelector.Init(_editor, -1, -1);
			_actIndexSelector.SelectedFrame = oldSelectedFrame;
			_actIndexSelector.DisableActionChange();

			_listBoxExtension.UpdateSelectionFromCommand(command);
		}

		private void InitializeFrames() {
			List<FrameIndexData> items = new List<FrameIndexData>();
			var action = _originalAction;

			for (int i = 0; i < action.Frames.Count; i++) {
				var frame = action[i];

				FrameIndexData data = new FrameIndexData();
				data.Index = i;
				data.OriginalIndex = i;
				data.DisplayName = frame.Layers.Count + " layer" + (frame.Layers.Count == 1 ? "" : "s");
				items.Add(data);
			}

			_actions.Clear();
			_actions.AddRange(items);
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			_originalAct.Commands.Backup(actInput => {
				_originalAction.Frames.Clear();

				foreach (var frame in _act[_selectedAction].Frames) {
					_originalAction.Frames.Add(new GRF.FileFormats.ActFormat.Frame(frame));
				}
			}, "Frame list changed");

			DialogResult = true;
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _miDelete_Click(object sender, RoutedEventArgs e) => _editListInteraction.Remove();
		private void _miPaste_Click(object sender, RoutedEventArgs e) => _editListInteraction.Paste();
		private void _miCopy_Click(object sender, RoutedEventArgs e) => _editListInteraction.Copy();
		private void _miCut_Click(object sender, RoutedEventArgs e) => _editListInteraction.Cut();
		private void _miMove_Click(object sender, RoutedEventArgs e) => _editListInteraction.MoveAt();
		private void _miInsert_Click(object sender, RoutedEventArgs e) => _editListInteraction.InsertAt();
		private void _miNewAction_Click(object sender, RoutedEventArgs e) => _editListInteraction.New(new FrameIndexData());
	}

	[Serializable]
	public class FrameIndexData : IEditData {
		public int Index { get; set; }
		public string DisplayName { get; set; }
		public string ChangeInfo {
			get {
				if (OriginalIndex < 0)
					return "New...";

				var diff = ChangeDiff;

				if (diff == 0)
					return "";

				if (diff < 0)
					return OriginalIndex + ", " + diff.ToString();
				else
					return OriginalIndex + ", " + "+" + diff.ToString();
			}
		}
		public int ChangeDiff {
			get {
				return Index - OriginalIndex;
			}
		}
		public bool IsChanged {
			get {
				return ChangeDiff != 0;
			}
		}
		public int OriginalIndex { get; set; }

		public IEditData Copy() {
			FrameIndexData data = new FrameIndexData();
			data.Index = Index;
			data.DisplayName = DisplayName;
			data.OriginalIndex = OriginalIndex;
			return data;
		}

		public override string ToString() {
			return Index + " | " + DisplayName + " [" + ChangeInfo + "]";
		}

		public FrameIndexData() {
			DisplayName = "New frame";
			OriginalIndex = -1;
			Index = -1;
		}
	}
}
