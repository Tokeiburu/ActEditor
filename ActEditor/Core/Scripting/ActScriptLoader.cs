using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using GRF.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;

namespace ActEditor.Core.Scripting {
	public class ActScriptLoader {
		private ActEditorWindow _actEditor;
		private IActScript _actScript;
		public MenuItem MenuItem { get; set; }
		public int FileNameHashCode { get; set; }
		public string FileDataHash { get; set; }
		public string DllDataHash { get; set; }

		public ActScriptLoader(IActScript actScript, ActEditorWindow actEditor) {
			_actEditor = actEditor;
			_actScript = actScript;
		}

		public void GenerateScriptMenu() {
			if (MenuItem != null)
				return;

			MenuItem = new MenuItem();
			MenuItem.Header = GenerateHeader(_actScript);

			AddInputGesture();
			AddIcon();
			MenuItem.Click += (s, e) => TryExecuteScript();
		}

		public void AddIcon() {
			if (_actScript.Image == null) {
				MenuItem.Icon = new Image { Source = ApplicationManager.PreloadResourceImage("empty.png") };
				return;
			}

			MenuItem.Icon = new Image { Source = GetScriptImage(_actScript.Image) };
			((Image)MenuItem.Icon).SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
		}

		public void AddInputGesture() {
			if (_actScript.InputGesture == null || MenuItem == null)
				return;

			if (_actScript.InputGesture.StartsWith("{")) {
				MenuItem.InputGestureText = "NA";

				var gestureCmd = _actScript.InputGesture.Trim('{', '}').Split('|');

				if (gestureCmd.Length > 1) {
					MenuItem.InputGestureText = gestureCmd[1];
				}

				MenuItem.Loaded += delegate {
					var gesture = ApplicationShortcut.GetGesture(gestureCmd[0]);

					ApplicationShortcut.Link(ApplicationShortcut.FromString(gestureCmd.Length > 1 ? gestureCmd[1] : "NULL", gestureCmd[0]), TryExecuteScript, _actEditor);

					gesture = ApplicationShortcut.GetGesture(gestureCmd[0]);

					if (gesture == null) {
						MenuItem.InputGestureText = "NA";
					}
					else {
						MenuItem.InputGestureText = ApplicationShortcut.FindDislayNameMenuItem(gesture);
					}

					MenuItem.InvalidateMeasure();
					MenuItem.InvalidateArrange();
				};
			}
			else {
				MenuItem.InputGestureText = _actScript.InputGesture.Split(new char[] { ':' }).FirstOrDefault();

				foreach (var gesture in _actScript.InputGesture.Split(':')) {
					ApplicationShortcut.Link(ApplicationShortcut.FromString(gesture, ((_actScript.DisplayName is string) ? _actScript.DisplayName.ToString() : gesture + "_cmd")), TryExecuteScript, _actEditor);
				}
			}
		}

		public void TryExecuteScript() {
			try {
				int actionIndex = -1;
				int frameIndex = -1;
				int[] selectedLayers = new int[] { };

				var tab = _actEditor.TabEngine.GetCurrentTab();

				if (tab == null) {
					return;
				}

				if (tab.Act != null) {
					actionIndex = tab._frameSelector.SelectedAction;
					frameIndex = tab._frameSelector.SelectedFrame;
					selectedLayers = tab.SelectionEngine.CurrentlySelected.OrderBy(p => p).ToArray();
				}

				if (_actScript.CanExecute(tab.Act, actionIndex, frameIndex, selectedLayers)) {
					int commandCount = -1;

					if (tab.Act != null) {
						commandCount = tab.Act.Commands.CommandIndex;
					}

					_actScript.Execute(tab.Act, actionIndex, frameIndex, selectedLayers);

					if (tab.Act != null) {
						if (tab.Act.Commands.CommandIndex == commandCount) {
							return;
						}
					}

					tab._frameSelector.Update();
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public static object GenerateHeader(IActScript actScript) {
			if (actScript.DisplayName is string) {
				string res = actScript.DisplayName.ToString();
				int indexOfEnd = res.IndexOf("__%", 0, StringComparison.Ordinal);

				if (indexOfEnd > -1)
					return res.Substring(indexOfEnd + 3);
				else
					return res;
			}

			return actScript.DisplayName;
		}

		public static ImageSource GetScriptImage(string image) {
			var im = ApplicationManager.PreloadResourceImage(image);

			if (im != null) {
				return im;
			}

			var path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath, image);

			if (File.Exists(path)) {
				byte[] data = File.ReadAllBytes(path);
				GrfImage grfImage = new GrfImage(data);
				return grfImage.Cast<BitmapSource>();
			}

			return null;
		}

		public void AddToEditor(Menu menu, ScriptLoaderResult scriptLoader = null) {
			if (MenuItem == null) {
				GenerateScriptMenu();
			}

			MenuItem parentMenuItem = GetParentMenuItem(_actScript, menu, scriptLoader);

			parentMenuItem.SubmenuOpened += delegate {
				int actionIndex = -1;
				int frameIndex = -1;
				int[] selectedLayers = new int[] { };

				var tab = _actEditor.TabEngine.GetCurrentTab();

				if (tab == null) {
					return;
				}

				if (tab.Act != null) {
					actionIndex = tab._frameSelector.SelectedAction;
					frameIndex = tab._frameSelector.SelectedFrame;
					selectedLayers = tab.SelectionEngine.CurrentlySelected.OrderBy(p => p).ToArray();
				}

				MenuItem.IsEnabled = _actScript.CanExecute(tab.Act, actionIndex, frameIndex, selectedLayers);
			};

			string[] parameters = GetParameter(_actScript, ScriptLoader.OverrideIndex);

			if (parameters != null && parameters.Length > 0) {
				int ival;

				if (Int32.TryParse(parameters[0], out ival)) {
					parentMenuItem.Items.Insert(ival, MenuItem);
				}
			}
			else {
				parentMenuItem.Items.Add(MenuItem);
			}
		}

		public static string[] GetParameter(IActScript actScript, string parameter) {
			string res = actScript.DisplayName as string;

			if (res == null) return null;

			int indexOfParam = res.IndexOf(parameter, 0, StringComparison.OrdinalIgnoreCase);

			if (indexOfParam > -1) {
				int indexOfEndParam = res.IndexOf("__", indexOfParam + parameter.Length, StringComparison.Ordinal);

				if (indexOfEndParam > -1) {
					string[] parameters = res.Substring(indexOfParam + parameter.Length, indexOfEndParam - (indexOfParam + parameter.Length)).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

					return parameters;
				}
			}

			return null;
		}

		public static MenuItem GetParentMenuItem(IActScript actScript, Menu menu, ScriptLoaderResult scriptLoader = null) {
			if ((actScript.Group.Contains("/") || actScript.Group.Contains("\\")) && scriptLoader?.AddedMenuItems == null) {
				// Script is requesting a submenu group
				string[] groups = actScript.Group.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

				List<MenuItem> menuItems = menu.Items.Cast<MenuItem>().ToList();
				MenuItem menuItem = null;
				ItemCollection parentCollection = menu.Items;

				foreach (string group in groups) {
					menuItem = menuItems.FirstOrDefault(p => GetMenuItemDisplayString(p) == group);

					if (menuItem == null) {
						if (group == groups[0])
							menuItem = CreateNewMenu(group);
						else
							menuItem = new MenuItem { Header = group, Icon = new Image { Source = ApplicationManager.PreloadResourceImage("empty.png") } };

						parentCollection.Add(menuItem);
					}

					menuItems = menuItem.Items.OfType<MenuItem>().ToList();
					parentCollection = menuItem.Items;
				}

				return menuItem;
			}

			{
				MenuItem menuItem = menu.Items.Cast<MenuItem>().FirstOrDefault(p => GetMenuItemDisplayString(p) == actScript.Group);

				if (menuItem != null) return menuItem;

				// Check if the MenuItem exists in the list of newly created items
				if (scriptLoader?.AddedMenuItems != null) {
					menuItem = scriptLoader.AddedMenuItems.FirstOrDefault(p => GetMenuItemDisplayString(p) == actScript.Group);

					if (menuItem != null) return menuItem;
				}

				menuItem = CreateNewMenu(actScript.Group);

				if (scriptLoader?.AddedMenuItems != null) {
					scriptLoader.AddedMenuItems.Add(menuItem);
				}
				else {
					menu.Items.Add(menuItem);
				}

				return menuItem;
			}
		}

		public static MenuItem CreateNewMenu(string display) {
			return new MenuItem { Header = new Label { Content = display, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-5, 0, -5, 0) } };
		}

		public static string GetMenuItemDisplayString(HeaderedItemsControl menuItem) {
			return menuItem.Header is Label header ? header.Content.ToString() : menuItem.Header.ToString();
		}
	}
}
