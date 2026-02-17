using ActEditor.ApplicationConfiguration;
using ActEditor.Core.ActionEditCommands;
using ActEditor.Core.DrawingComponents;
using ActEditor.Core.WPF.FrameEditor;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GrfToWpfBridge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ActionEditDialog.xaml
	/// </summary>
	public partial class ActionEditDialog : TkWindow {
		private Act _originalAct;
		private Act _act;
		private RangeObservableCollection<IEditData> _actions = new RangeObservableCollection<IEditData>();
		private DummyFrameEditor _editor;
		private CommandsHolder _commands;
		private SelectableListBoxExtension _listBoxExtension;
		private ActEditListInteraction _editListInteraction;
		public RangeObservableCollection<IEditData> Actions => _actions;

		public ActionEditDialog() : base("Action edit", "advanced.png") {
			InitializeComponent();

			_commands = new CommandsHolder(_actions);
			_listBoxExtension = new SelectableListBoxExtension(this, _lvActions, _gridEvents, _rectSelection, _rectInsertion, 130, 18);
			_editListInteraction = new ActEditListInteraction(_commands, this, _lvActions, _listBoxExtension);
		}

		public ActionEditDialog(Act act, int index) : this() {
			_originalAct = act;
			_act = new Act(act);

			InitializeActions();
			InitializeEditor(index);
			InitializeShortcuts();

			DataContext = this;

			_lvActions.SelectionChanged += _lvActions_SelectionChanged;
			_commands.ModifiedStateChanged += _commands_ModifiedStateChanged;

			if (index >= 0 && index < _actions.Count) {
				_lvActions.SelectedItem = _actions[index];

				Loaded += delegate {
					_lvActions.ScrollToCenterOfView(_actions[index]);
				};
			}
		}

		public void InitializeShortcuts() {
			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-N", "ListData.New"), () => _editListInteraction.New(new ActionIndexData()), this);
		}

		private void _lvActions_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_lvActions.SelectedItem != null) {
				_actIndexSelector.SelectedAction = ((IEditData)_lvActions.SelectedItem).Index;
			}
		}

		public void InitializeEditor(int index) {
			_editor = DummyFrameEditor.CreateEditor(() => _act, this, _actIndexSelector, _rfp, selectedAction: index, playAnimation: true);

			Owner = ActEditorWindow.Instance;

			Closing += delegate {
				ActEditorWindow.Instance.Focus();
			};
		}

		private void _commands_ModifiedStateChanged(object sender, IActionEditCommand<IEditData> command) {
			// Update the data list!
			_actions.Disable();

			for (int i = 0; i < _actions.Count; i++) {
				_actions[i].Index = i;
			}

			_actions.UpdateAndEnable();

			// Update editor preview
			var act = new Act(_act);
			act.Actions.Clear();

			for (int i = 0; i < _actions.Count; i++) {
				var index = ((ActionIndexData)_actions[i]).OriginalIndex;

				if (index < 0)
					act.Actions.Add(new GRF.FileFormats.ActFormat.Action());
				else
					act.Actions.Add(new GRF.FileFormats.ActFormat.Action(_originalAct[index]));
			}

			_act = act;
			_actIndexSelector.Init(_editor, _actIndexSelector.SelectedAction, _actIndexSelector.SelectedFrame);

			_listBoxExtension.UpdateSelectionFromCommand(command);
		}

		private void InitializeActions() {
			List<ActionIndexData> items = new List<ActionIndexData>();
			var animations = ActHelper.GetAnimations(_act);

			for (int i = 0; i < _act.Actions.Count; i++) {
				var animation = animations[i / 8];

				var idx = animation.IndexOf("-");

				if (idx > -1)
					animation = animation.Substring(idx + 2);

				ActionIndexData data = new ActionIndexData();
				data.Index = i;
				data.OriginalIndex = i;
				data.DisplayName = animation;
				data.Direction = i % 8;
				items.Add(data);
			}

			_actions.Clear();
			_actions.AddRange(items);
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			_originalAct.Commands.Backup(actInput => {
				actInput.Actions.Clear();

				foreach (var action in _act.Actions) {
					actInput.Actions.Add(new GRF.FileFormats.ActFormat.Action(action));
				}
			}, "Action list changed");

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
		private void _miNewAction_Click(object sender, RoutedEventArgs e) => _editListInteraction.New(new ActionIndexData());
	}

	[Serializable]
	public class ActionIndexData : IEditData {
		public int Index { get; set; }
		public string DisplayName { get; set; }
		public int Direction { get; set; }
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
			ActionIndexData data = new ActionIndexData();
			data.Index = Index;
			data.DisplayName = DisplayName;
			data.Direction = Direction;
			data.OriginalIndex = OriginalIndex;
			return data;
		}

		public override string ToString() {
			return Index + " | " + DisplayName + " | " + Direction + " [" + ChangeInfo + "]";
		}

		public ActionIndexData() {
			DisplayName = "New action";
			OriginalIndex = -1;
			Index = -1;
		}
	}
}
