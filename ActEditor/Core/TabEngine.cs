using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Utilities;
using Utilities.Services;
using ActEditor.Components;

namespace ActEditor.Core {
	public class TabEngine {
		private readonly TabControl _tabControl;
		private readonly ActEditorWindow _actEditor;
		public string LastOpened { get; set; }
		private ActSaveService _actSaveService = new ActSaveService();
		private ActLoadService _actLoadService = new ActLoadService();

		public TabEngine(TabControl tabControl, ActEditorWindow editor) {
			_tabControl = tabControl;
			_actEditor = editor;
			_tabControl.SelectionChanged += _tabControl_SelectionChanged;
		}

		private void _tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_tabControl.SelectedIndex > 0 && e.AddedItems.Count > 0 && e.AddedItems[0] is TabAct) {
				var tab = (TabAct)e.AddedItems[0];

				if (!tab.IsActLoaded) {
					tab.OnActLoaded();
				}
			}
		}

		public void Copy() {
			_getCurrentTab()?.Copy();
		}

		public void Paste() {
			_getCurrentTab()?.Paste();
		}

		public void Cut() {
			_getCurrentTab()?.Cut();
		}

		public TabAct GetCurrentTab() {
			return _getCurrentTab();
		}

		private TabAct _getCurrentTab() {
			return _tabControl.SelectedItem as TabAct;
		}

		public List<TabAct> GetTabs() {
			return _tabControl.Items.OfType<TabAct>().ToList();
		}

		public void SetAnchorIndex(int anchorIndex) {
			foreach (var tab in GetTabs()) {
				tab.SetAnchorIndex(anchorIndex);
			}
		}

		public void RendererUpdate() {
			_getCurrentTab()?.UpdatePrimary();
		}

		public void ShowPreviewFrames() {
			_getCurrentTab()?.ShowOrDisablePreviewFrames();
		}

		public void ReverseAnchorChecked() {
			ActEditorConfiguration.ReverseAnchor = true;

			foreach (var tab in GetTabs()) {
				tab.ReverseAnchorChecked();
			}
		}

		public void ReverseAnchorUnchecked() {
			ActEditorConfiguration.ReverseAnchor = false;

			foreach (var tab in GetTabs()) {
				tab.ReverseAnchorUnchecked();
			}
		}

		public bool SaveAs(TabAct tab = null) {
			tab = tab ?? _getCurrentTab();

			if (tab == null)
				return false;

			try {
				var context = _actSaveService.CreateSaveContext(tab);
				if (context == null) return false;

				var result = _actSaveService.ExecuteSave(context);

				if (result.IsNewCleared)
					tab.IsNew = false;

				if (result.AddToRecentFiles)
					_actEditor.RecentFiles.AddRecentFile(result.NewFilePath);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				RestoreFocus();
			}

			return false;
		}

		public void RestoreFocus() {
			_getCurrentTab()?.Focus();
		}

		public TabAct CreateTab(Act act, bool isNew) {
			TabAct tab = new TabAct(_actEditor);
			tab.Style = _actEditor.FindResource("TabItemSprite") as Style;
			tab.Act = act;
			tab.IsNew = isNew;
			tab.Header = Path.GetFileNameWithoutExtension(new TkPath(act.LoadedPath).FileName);

			_tabControl.Items.Add(tab);
			_tabControl.Visibility = Visibility.Visible;

			if (_tabControl.Items.Count == 2) {
				if (((TabAct)_tabControl.Items[0]).Header.ToString() == "new_0000 *") {
					_closeAct(((TabAct)_tabControl.Items[0]).Act);
					_tabControl.Items.Remove(_tabControl.Items[0]);
					_tabControl.SelectedIndex = -1;
				}
			}

			_addEvents(tab);
			SetTabHeaderTitle(tab);
			return tab;
		}

		public bool CloseActOnly(Act act) {
			return _closeAct(act);
		}

		private bool _closeAct(Act act) {
			if (act != null && act.Commands.IsModified) {
				var res = WindowProvider.ShowDialog("The ACT has been modified, would you like to save it first?\n\n" + act.LoadedPath, "Modified Act - " + Path.GetFileNameWithoutExtension(act.LoadedPath), MessageBoxButton.YesNoCancel);

				if (res == MessageBoxResult.Yes) {
					if (!Save()) {
						return false;
					}
				}

				if (res == MessageBoxResult.Cancel) {
					return false;
				}
			}

			if (act != null) {
				act.Commands.ClearCommands();
			}

			return true;
		}

		public void SetTabHeaderTitle(TabAct tab) {
			string name = Path.GetFileNameWithoutExtension(tab.Act.LoadedPath);

			tab.BeginDispatch(delegate {
				if (!tab.Act.Commands.IsModified && !tab.IsNew) {
					tab.Header = name;
				}
				if (!tab.Act.Commands.IsModified && tab.IsNew) {
					tab.Header = name + " *";
				}
				else if (tab.Act.Commands.IsModified || tab.IsNew) {
					tab.Header = name + " *";
				}
			});
		}

		private void _addEvents(TabAct tab) {
			tab.Act.Commands.CommandIndexChanged += delegate {
				SetTabHeaderTitle(tab);
			};

			tab.NewStateChanged += delegate {
				SetTabHeaderTitle(tab);
			};
		}

		public void OpenFiles(TkPath[] files) {
			foreach (var file in files) {
				Open(file, focusTab: false);
			}

			Focus(LastOpened);
		}

		public void _open(TkPath file, bool isNew = false, bool focusTab = true) {
		}

		public void Open(TkPath file, bool isNew = false, bool focusTab = true) {
			try {
				if (TabExists(file))
					return;

				var result = _actLoadService.Load(file);

				if (result.AddToRecentFiles)
					_actEditor.RecentFiles.AddRecentFile(result.FilePath);
				if (result.RemoveToRecentFiles)
					_actEditor.RecentFiles.RemoveRecentFile(result.FilePath);
				if (result.ErrorMessage != null)
					ErrorHandler.HandleException(result.ErrorMessage);
				if (!result.Success)
					return;

				TabAct tab = CreateTab(result.LoadedAct, isNew);

				if (_tabControl.SelectedIndex == -1) {
					tab.OnActLoaded();
					tab.UpdatePrimary();
				}

				LastOpened = result.FilePath;

				if (focusTab)
					Focus(tab);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public bool TabExists(TkPath file) {
			var fullPath = file.GetFullPath();

			// Check if the path is already loaded
			foreach (var tabS in GetTabs()) {
				if (tabS.Act.LoadedPath == fullPath) {
					tabS.IsSelected = true;
					return true;
				}
			}

			return false;
		}

		public void CloseAct() {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			if (_closeAct(tab.Act)) {
				_tabControl.Items.Remove(tab);

				if (_tabControl.Items.Count == 0) {
					_tabControl.Visibility = Visibility.Collapsed;
				}
			}
		}

		public bool CloseAct(TabAct tab) {
			if (_closeAct(tab.Act)) {
				tab.Close();
				_tabControl.Items.Remove(tab);

				if (_tabControl.Items.Count == 0) {
					_tabControl.Visibility = Visibility.Collapsed;
				}

				return true;
			}

			return false;
		}

		public bool Save(TabAct tab = null) {
			tab = tab ?? _getCurrentTab();

			if (tab == null)
				return false;

			try {
				if (tab.Act != null) {
					if (tab.IsNew)
						return SaveAs();

					var result = _actSaveService.Save(tab.Act);

					if (result.IsNewCleared)
						tab.IsNew = false;
					
					return true;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				RestoreFocus();
			}

			return false;
		}

		public void Select(TabAct tab = null) {
			tab = tab ?? _getCurrentTab();

			if (tab == null) {
				ErrorHandler.HandleException("No act loaded.");
				return;
			}

			try {
				TkPath path = new TkPath(tab.Act.LoadedPath);
				OpeningService.FilesOrFolders(path.FilePath);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void Undo() {
			_getCurrentTab()?.Undo();
		}

		public void Redo() {
			_getCurrentTab()?.Redo();
		}

		public void FrameMove(int amount) {
			_getCurrentTab()?.FrameMove(amount);
		}

		public void ActionMove(int amount) {
			_getCurrentTab()?.ActionMove(amount);
		}

		public void Execute(Action<TabAct> action) {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			action(tab);
		}

		public void Focus(TabAct tab) {
			tab.IsSelected = true;
		}

		public void Focus(string file) {
			if (file == "")
				return;

			foreach (var tab in GetTabs()) {
				if (tab.Act.LoadedPath == file) {
					tab.IsSelected = true;
					return;
				}
			}
		}
	}
}
