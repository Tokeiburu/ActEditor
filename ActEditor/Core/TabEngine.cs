using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.Core;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GRF.System;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Extension;
using Utilities.Services;
using Imaging = ActImaging.Imaging;

namespace ActEditor.Core {
	public class TabEngine {
		private readonly TabControl _tabControl;
		private readonly ActEditorWindow _actEditor;
		public string LastOpened { get; set; }

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
			TabAct tab = _getCurrentTab();

			if (tab != null)
				tab._rendererPrimary.Copy();
		}

		public void Paste() {
			TabAct tab = _getCurrentTab();

			if (tab != null)
				tab._rendererPrimary.Paste();
		}

		public void Cut() {
			TabAct tab = _getCurrentTab();

			if (tab != null)
				tab._rendererPrimary.Cut();
		}

		public TabAct GetCurrentTab() {
			return _getCurrentTab();
		}

		private TabAct _getCurrentTab() {
			if (_tabControl.SelectedItem != null) {
				return (TabAct)_tabControl.SelectedItem;
			}

			return null;
		}

		public void AnchorUpdate(MenuItem sender) {
			foreach (var tab in _tabControl.Items.OfType<TabAct>()) {
				if (tab._rendererPrimary != null) {
					tab._rendererPrimary.AnchorModule.AnchorIndex = Int32.Parse((sender).Tag.ToString());
					tab._rendererPrimary.Update();
				}
			}
		}

		public void RendererUpdate() {
			TabAct tab = _getCurrentTab();

			if (tab != null) {
				tab._rendererPrimary.Update();
			}
		}

		public void ShowPreviewFrames() {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			if (tab._rendererLeft.IsHitTestVisible || tab._rendererRight.IsHitTestVisible) {
				tab._col0.Width = new GridLength(0);
				tab._col2.Width = new GridLength(0);

				tab._rendererLeft.Visibility = Visibility.Collapsed;
				tab._rendererLeft.IsHitTestVisible = false;
				tab._rendererRight.Visibility = Visibility.Collapsed;
				tab._rendererRight.IsHitTestVisible = false;
				tab._col1.Width = new GridLength(1, GridUnitType.Star);
			}
			else {
				tab.CreatePreviewGrid(true);
				tab.CreatePreviewGrid(false);
				tab._col1.Width = new GridLength(1, GridUnitType.Star);
			}
		}

		public void ReverseAnchorChecked() {
			ActEditorConfiguration.ReverseAnchor = true;

			foreach (var tab in _tabControl.Items.OfType<TabAct>()) {
				if (tab.Act != null) {
					foreach (var reference in tab.References) {
						if (reference.Act != null && reference.Act.Name == "Body") {
							tab.Act.AnchoredTo = reference.Act;
							reference.Act.AnchoredTo = null;
							break;
						}
					}
				}

				tab._rendererLeft.Update();
				tab._rendererPrimary.Update();
				tab._rendererRight.Update();
			}
		}

		public void ReverseAnchorUnchecked() {
			ActEditorConfiguration.ReverseAnchor = false;

			foreach (var tab in _tabControl.Items.OfType<TabAct>()) {
				if (tab.Act != null) {
					tab.Act.AnchoredTo = null;

					foreach (var reference in tab.References) {
						if (reference.Act != null && reference.Act.Name == "Body") {
							reference.RefreshSelection();
							break;
						}
					}
				}

				tab._rendererLeft.Update();
				tab._rendererPrimary.Update();
				tab._rendererRight.Update();
			}
		}

		public bool SaveAs(TabAct tab = null) {
			tab = tab ?? _getCurrentTab();

			if (tab == null)
				return false;

			try {
				if (tab.Act != null) {
					var fileName = ActEditorConfiguration.AppLastPath;

					if (Path.GetFileNameWithoutExtension(fileName) != Path.GetFileNameWithoutExtension(tab.Act.LoadedPath)) {
						fileName = tab.Act.LoadedPath;
					}

					string file = TkPathRequest.SaveFile<ActEditorConfiguration>("AppLastPath", "fileName", fileName, "filter", "Act and Spr files|*.act;*.spr|Act, Spr and Pal files|*.act;*.spr;*.pal|" + FileFormat.MergeFilters(Format.Act | Format.Spr | Format.Gif | Format.Pal | Format.Image));

					if (file != null) {
						var sfd = TkPathRequest.LatestSaveFileDialog;

						if (sfd.FilterIndex == 1 && file.IsExtension(".act", ".spr")) {
							tab.Act.SaveWithSprite(file.ReplaceExtension(".act"));
							_actEditor.RecentFiles.AddRecentFile(file);
							tab.Act.LoadedPath = file.ReplaceExtension(".act");
							tab.Act.Commands.SaveCommandIndex();
							tab.IsNew = false;
							SetTitle(tab);
							return true;
						}
						else if ((sfd.FilterIndex == 2 && file.IsExtension(".act"))) {
							tab.Act.SaveWithSprite(file.ReplaceExtension(".act"));
							File.WriteAllBytes(file.ReplaceExtension(".pal"), tab.Act.Sprite.Palette.BytePalette);
							_actEditor.RecentFiles.AddRecentFile(file);
							tab.Act.LoadedPath = file.ReplaceExtension(".act");
							tab.Act.Commands.SaveCommandIndex();
							tab.IsNew = false;
							SetTitle(tab);
						}
						else if ((sfd.FilterIndex == 3 && file.IsExtension(".act")) || file.IsExtension(".act")) {
							tab.Act.Save(file.ReplaceExtension(".act"));
							_actEditor.RecentFiles.AddRecentFile(file);
						}
						else if ((sfd.FilterIndex == 4 && file.IsExtension(".pal")) || file.IsExtension(".pal")) {
							File.WriteAllBytes(file, tab.Act.Sprite.Palette.BytePalette);
						}
						else if ((sfd.FilterIndex == 5 && file.IsExtension(".spr")) || file.IsExtension(".spr")) {
							tab.Act.Sprite.Converter.Save(tab.Act.Sprite, file.ReplaceExtension(".spr"));
						}
						else if ((sfd.FilterIndex == 6 && file.IsExtension(".gif")) || file.IsExtension(".gif")) {
							try {
								for (int i = 0; i < tab.Act.Sprite.NumberOfIndexed8Images; i++) {
									tab.Act.Sprite.Images[i].Palette[3] = 0;
								}

								GifSavingDialog dialog = new GifSavingDialog(tab.Act, tab.SelectedAction);
								dialog.Owner = WpfUtilities.TopWindow;

								if (ActEditorConfiguration.ActEditorGifHideDialog || dialog.ShowDialog() == true) {
									TaskDialog task = new TaskDialog("Saving as gif...", "app.ico", "Processing frames, please wait...");
									task.ShowFooter(true);
									task.Start(isCancelling => {
										try {
											List<Act> back = new List<Act>();
											List<Act> front = new List<Act>();

											foreach (var reference in tab.References.Where(reference => reference.ShowReference)) {
												if (reference.Mode == ZMode.Back)
													back.Insert(0, reference.Act);
												else
													front.Add(reference.Act);
											}

											Imaging.SaveAsGif(file, Act.MergeAct(back.ToArray(), tab.Act, front.ToArray()), tab.SelectedAction, task, dialog.Dispatch(() => dialog.Extra));
										}
										catch (Exception err) {
											ErrorHandler.HandleException(err);
										}
									}, () => task.Progress);
									task.ShowDialog();
								}
							}
							finally {
								for (int i = 0; i < tab.Act.Sprite.NumberOfIndexed8Images; i++) {
									tab.Act.Sprite.Images[i].Palette[3] = 255;
								}
							}
						}
						else {
							if (!file.IsExtension(".bmp", ".png", ".jpg", ".tga")) {
								ErrorHandler.HandleException("Invalid file extension.");
								return false;
							}

							var imgSource = Imaging.GenerateImage(tab.Act, tab.SelectedAction, tab.SelectedFrame);
							PngBitmapEncoder encoder = new PngBitmapEncoder();
							encoder.Frames.Add(BitmapFrame.Create(Imaging.ForceRender(imgSource, BitmapScalingMode.NearestNeighbor)));

							using (MemoryStream stream = new MemoryStream()) {
								encoder.Save(stream);

								byte[] data = new byte[stream.Length];
								stream.Seek(0, SeekOrigin.Begin);
								stream.Read(data, 0, data.Length);

								GrfImage grfImage = new GrfImage(ref data);
								GrfToWpfBridge.Imaging.Save(grfImage, file);
							}
						}
					}
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

		public void RestoreFocus() {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			tab.Focus();
		}

		public void OpenFromFile(string file, bool isNew) {
			try {
				if (!file.IsExtension(".act")) {
					_actEditor.RecentFiles.RemoveRecentFile(file);
					ErrorHandler.HandleException("Invalid file extension; only .act files are allowed.");
					return;
				}

				if (!File.Exists(file)) {
					_actEditor.RecentFiles.RemoveRecentFile(file);
					ErrorHandler.HandleException("File not found while trying to open the Act.\r\n\r\n" + file);
					return;
				}

				_actEditor.RecentFiles.AddRecentFile(file);

				if (!File.Exists(file.ReplaceExtension(".spr"))) {
					_actEditor.RecentFiles.RemoveRecentFile(file);
					ErrorHandler.HandleException("File not found : " + file.ReplaceExtension(".spr"));
					return;
				}

				foreach (var tabS in _tabControl.Items.OfType<TabAct>()) {
					if (tabS.Act.LoadedPath == file) {
						tabS.IsSelected = true;
						return;
					}
				}

				var act = new Act(file, file.ReplaceExtension(".spr"));
				act.LoadedPath = file;

				TabAct tab = _addNewTab(act);
				tab.IsNew = isNew;
				_addEvents(tab);

				if (_tabControl.SelectedIndex == -1) {
					try {
						tab._rendererPrimary.DisableUpdate = true;
						tab.OnActLoaded();
						tab._rendererPrimary.Update();
					}
					finally {
						tab._rendererPrimary.DisableUpdate = false;
					}
				}

				LastOpened = file;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private TabAct _addNewTab(Act act) {
			TabAct tab = new TabAct(_actEditor);
			tab.Style = _actEditor.FindResource("TabItemSprite") as Style;
			tab.Act = act;

			tab.Header = Path.GetFileNameWithoutExtension(new TkPath(act.LoadedPath).FileName);

			_tabControl.Items.Add(tab);
			
			tab.Loaded += delegate {
				var border = WpfUtilities.FindChild<Border>(tab, "_borderButton");

				if (border != null) {
					Grid parent = border.Parent as Grid;

					if (parent != null) {
						ToolTip tt = new ToolTip();
						tt.Content = act.LoadedPath;
						parent.ToolTip = tt;
						parent.ContextMenu = new ContextMenu();

						TkMenuItem miClose = new TkMenuItem();
						miClose.Click += delegate {
							CloseAct(tab);
						};
						miClose.Header = "Close Act";
						miClose.InputGestureText = "Ctrl-Q";
						miClose.Icon = new Image { Source = ApplicationManager.GetResourceImage("delete.png") };

						TkMenuItem miSelect = new TkMenuItem();
						miSelect.Click += delegate {
							Select(tab);
						};
						miSelect.Header = "Select Act";
						miSelect.Icon = new Image { Source = ApplicationManager.GetResourceImage("arrowdown.png") };

						TkMenuItem miSave = new TkMenuItem();
						miSave.Click += delegate {
							Save(tab);
						};
						miSave.Header = "Save";
						miSave.InputGestureText = "Ctrl-S";
						miSave.Icon = new Image { Source = ApplicationManager.GetResourceImage("save.png") };

						TkMenuItem miSaveAs = new TkMenuItem();
						miSaveAs.Click += delegate {
							SaveAs(tab);
						};
						miSaveAs.Header = "Save as...";
						//miSaveAs.InputGestureText = "Ctrl-S";
						//miSaveAs.Icon = new Image { Source = ApplicationManager.GetResourceImage("save.png") };

						TkMenuItem miCloseAllBut = new TkMenuItem();
						miCloseAllBut.Click += delegate {
							var tabs = _tabControl.Items.OfType<TabAct>().ToList();

							foreach (var tabS in tabs) {
								if (!tabS.Act.Commands.IsModified && tabS != tab) {
									if (!CloseAct(tabS)) {
										return;
									}
								}
							}
						};
						miCloseAllBut.Header = "Close all but this";

						parent.ContextMenu.Items.Add(miSave);
						parent.ContextMenu.Items.Add(miSaveAs);
						parent.ContextMenu.Items.Add(new Separator());
						parent.ContextMenu.Items.Add(miCloseAllBut);
						parent.ContextMenu.Items.Add(new Separator());
						parent.ContextMenu.Items.Add(miClose);
						parent.ContextMenu.Items.Add(miSelect);

						parent.MouseDown += (s, e) => {
							if (e.MiddleButton == MouseButtonState.Pressed) {
								CloseAct(tab);
							}
						};
					}

					border.PreviewMouseLeftButtonDown += (e, a) => { a.Handled = true; };
					border.PreviewMouseLeftButtonUp += (e, a) => {
						if (_closeAct(act)) {
							_tabControl.Items.Remove(tab);

							if (_tabControl.Items.Count == 0) {
								_tabControl.Visibility = Visibility.Collapsed;
							}
						}
					};
				}
			};

			//tab.IsSelected = true;

			if (_tabControl.Items.Count > 0) {
				_tabControl.Visibility = Visibility.Visible;
			}

			if (_tabControl.Items.Count == 2) {
				if (((TabAct)_tabControl.Items[0]).Header.ToString() == "new_0000 *") {
					_closeAct(((TabAct)_tabControl.Items[0]).Act);
					_tabControl.Items.Remove(_tabControl.Items[0]);
					_tabControl.SelectedIndex = -1;
				}
			}

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

		public void SetTitle(TabAct tab) {
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
			SetTitle(tab);

			tab.Act.Commands.CommandIndexChanged += delegate {
				SetTitle(tab);
			};
		}

		public void Open(TkPath file, bool isNew) {
			try {
				if (file.FilePath.IsExtension(".act") || String.IsNullOrEmpty(file.RelativePath)) {
					OpenFromFile(file.FilePath, isNew);
					return;
				}

				if (!File.Exists(file.FilePath)) {
					_actEditor.RecentFiles.RemoveRecentFile(file.GetFullPath());
					return;
				}

				_actEditor.RecentFiles.AddRecentFile(file.GetFullPath());

				TkPath sprPath = new TkPath(file);
				sprPath.RelativePath = sprPath.RelativePath.ReplaceExtension(".spr");

				byte[] dataAct = null;
				byte[] dataSpr = null;

				using (GrfHolder grf = new GrfHolder(file.FilePath)) {
					if (grf.FileTable.ContainsFile(file.RelativePath))
						dataAct = grf.FileTable[file.RelativePath].GetDecompressedData();

					if (grf.FileTable.ContainsFile(file.RelativePath.ReplaceExtension(".spr")))
						dataSpr = grf.FileTable[file.RelativePath.ReplaceExtension(".spr")].GetDecompressedData();
				}

				if (dataAct == null) {
					ErrorHandler.HandleException("File not found : " + file);
					return;
				}

				if (dataSpr == null) {
					ErrorHandler.HandleException("File not found : " + sprPath);
					return;
				}

				var fullPath = file.GetFullPath();

				foreach (var tabS in _tabControl.Items.OfType<TabAct>()) {
					if (tabS.Act.LoadedPath == fullPath) {
						tabS.IsSelected = true;
						return;
					}
				}

				var act = new Act(dataAct, dataSpr);
				act.LoadedPath = fullPath;

				var tab = _addNewTab(act);
				tab.IsNew = isNew;
				_addEvents(tab);
				LastOpened = fullPath;

				try {
					tab._rendererPrimary.DisableUpdate = true;
					tab.OnActLoaded();
					tab._rendererPrimary.Update();
				}
				finally {
					tab._rendererPrimary.DisableUpdate = false;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
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
					if (tab.IsNew) {
						return SaveAs();
					}

					TkPath path = new TkPath(tab.Act.LoadedPath);

					if (!String.IsNullOrEmpty((path.RelativePath))) {
						if (Methods.IsFileLocked(path.FilePath)) {
							ErrorHandler.HandleException("The file " + path.FilePath + " is locked by another process. Try closing other GRF applicactions or use the 'Save as...' option.");
							return false;
						}

						using (GrfHolder grf = new GrfHolder(path.FilePath)) {
							string temp = TemporaryFilesManager.GetTemporaryFilePath("to_grf_{0:0000}");

							tab.Act.Sprite.Save(temp + ".spr");
							tab.Act.Save(temp + ".act");

							grf.Commands.AddFile(path.RelativePath.ReplaceExtension(".act"), File.ReadAllBytes(temp + ".act"));
							grf.Commands.AddFile(path.RelativePath.ReplaceExtension(".spr"), File.ReadAllBytes(temp + ".spr"));

							grf.QuickSave();

							if (!grf.CancelReload) {
								tab.Act.Commands.SaveCommandIndex();
							}
						}
					}
					else {
						tab.Act.Sprite.Save(tab.Act.LoadedPath.ReplaceExtension(".spr"));
						tab.Act.Save();
						tab.Act.Commands.SaveCommandIndex();
					}

					tab.IsNew = false;
					SetTitle(tab);
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
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			tab.Act.Commands.Undo();
		}

		public void Redo() {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			tab.Act.Commands.Redo();
		}

		public void FrameMove(int amount) {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			tab._frameSelector.SelectedFrame += amount;

			while (tab._frameSelector.SelectedFrame >= tab.Act[tab._frameSelector.SelectedAction].NumberOfFrames) {
				tab._frameSelector.SelectedFrame -= tab.Act[tab._frameSelector.SelectedAction].NumberOfFrames;
			}

			while (tab._frameSelector.SelectedFrame < 0) {
				tab._frameSelector.SelectedFrame += tab.Act[tab._frameSelector.SelectedAction].NumberOfFrames;
			}

			tab._frameSelector.SetFrame(tab.SelectedFrame);
			//tab._frameSelector.OnFrameChanged(tab.SelectedAction);
		}

		public void ActionMove(int amount) {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			tab._frameSelector.SelectedAction += amount;

			while (tab._frameSelector.SelectedAction >= tab.Act.NumberOfActions) {
				tab._frameSelector.SelectedAction -= tab.Act.NumberOfActions;
			}

			while (tab._frameSelector.SelectedAction < 0) {
				tab._frameSelector.SelectedAction += tab.Act.NumberOfActions;
			}

			tab._frameSelector.SetAction(tab.SelectedAction);
		}

		public void Execute(Action<TabAct> action) {
			TabAct tab = _getCurrentTab();

			if (tab == null)
				return;

			action(tab);
		}

		public void Focus(string file) {
			if (file == "")
				return;

			foreach (var tabS in _tabControl.Items.OfType<TabAct>()) {
				if (tabS.Act.LoadedPath == file) {
					tabS.IsSelected = true;
					return;
				}
			}
		}
	}
}
