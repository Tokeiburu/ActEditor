using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.IO;
using GRF.Image;
using GRF.GrfSystem;
using GRF.Threading;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using Utilities;
using Utilities.Extension;
using Utilities.Hash;
using Action = System.Action;
using Debug = Utilities.Debug;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using System.Text;

namespace ActEditor.Core {
	/// <summary>
	/// The ScriptLoader class manages the scripts used by the software.
	/// </summary>
	public class ScriptLoader : IDisposable {
		public const string OutputPath = "Scripts";
		public const string OverrideIndex = "__IndexOverride";
		internal static string[] ScriptNames = { "script_sample", "script0_magnify", "script1_replace_color", "script1_replace_color_all", "script2_expand", "script4_generate_single_sprite", "script5_remove_unused_sprites", "script6_merge_layers", "script7_add_effect1", "script8_add_frames", "script9_chibi_grf", "script10_trim_images", "script11_palette_sheet", "script12_remove_unused_palette",  };
		internal static string[] Libraries = {"GRF.dll", "Utilities.dll", "TokeiLibrary.dll", "ErrorManager.dll"};
		private static ConfigAsker _librariesConfiguration;
		private FileSystemWatcher _fsw;
		private HashSet<MenuItem> _initialMenuItems = new HashSet<MenuItem>();
		private object _lock = new object();
		private int _procId;
		private ActEditorWindow _actEditor;
		private DockPanel _dockPanel;
		private Menu _menu;
		private static Dictionary<string, Dictionary<string, IActScript>> _compiledScripts = new Dictionary<string, Dictionary<string, IActScript>>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptLoader" /> class.
		/// </summary>
		public ScriptLoader(Menu menu, FrameworkElement dockPanel) {
			_initializeFileWatcher();

			ApplicationManager.ThemeChanged += delegate {
				_updateRedoUndoLeftMargin(menu, dockPanel);
			};
		}

		/// <summary>
		/// Initializes the file system watcher.
		/// </summary>
		private void _initializeFileWatcher() {
			_fsw = new FileSystemWatcher();

			string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath);

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			_procId = Process.GetCurrentProcess().Id;
			_fsw.Path = path;
			_fsw.Filter = "*.cs";
			_fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
			_fsw.Changed += _fileChanged;
			_fsw.Created += _fileChanged;
			_fsw.Renamed += _fileChanged;
			_fsw.Deleted += _fileChanged;
			_fsw.EnableRaisingEvents = true;
		}

		/// <summary>
		/// Gets the ConfigAsker for the compiled libraries.
		/// </summary>
		public static ConfigAsker LibrariesConfiguration {
			get { return _librariesConfiguration ?? (_librariesConfiguration = new ConfigAsker(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath, "scripts.dat"))); }
		}

		/// <summary>
		/// Gets a list of all the custom compiled scripts from the roaming folder.
		/// </summary>
		public static Dictionary<string, Dictionary<string, IActScript>> CompiledScripts => _compiledScripts;

		/// <summary>
		/// Gets a value indicating whether exceptions should be conbined or directly shown when they occur.
		/// </summary>
		public bool CombineErrors { get; set; }

		public List<Exception> PendingErrors = new List<Exception>();
		private bool _pendingReload = false;

		/// <summary>
		/// Reloads the scripts.
		/// </summary>
		public void ReloadScripts() {
			if (_actEditor == null || _menu == null) return;
			_compiledScripts.Clear();

			if (_pendingReload)
				return;

			_pendingReload = true;

			if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) {
				GrfThread.StartSTA(delegate {
					try {
						AddScriptsToMenu(_actEditor, _menu, _dockPanel);
					}
					finally {
						_pendingReload = false;
					}
				});
			}
			else {
				AddScriptsToMenu(_actEditor, _menu, _dockPanel);
				_pendingReload = false;
			}
		}

		/// <summary>
		/// Recompiles the scripts.
		/// </summary>
		public void RecompileScripts() {
			// Deleting the config asker's properties will
			// force a full reload of all the scripts.
			LibrariesConfiguration.DeleteKeys("");
			ReloadScripts();
		}

		/// <summary>
		/// Reloads the libraries.
		/// </summary>
		public void ReloadLibraries() {
			try {
				Libraries.ToList().ForEach(v => Debug.Ignore(() => File.WriteAllBytes(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath, v), ApplicationManager.GetResource(v, true))));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		/// <summary>
		/// Adds a native script object in the menu (not from the Scripts folder).
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="menu">The menu.</param>
		/// <param name="dockPanel">The dock panel, this value can be null.</param>
		public void AddScriptsToMenu(IActScript actScript, ActEditorWindow actEditor, Menu menu, FrameworkElement dockPanel) {
			_setupMenuItem(actScript, menu, actEditor, _generateScriptMenu(actEditor, actScript, false), null);
			_updateRedoUndoLeftMargin(menu, dockPanel);
		}

		/// <summary>
		/// Adds all the scripts (from the Scripts folder) to the menu.
		/// </summary>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="menu">The menu.</param>
		/// <param name="dockPanel">The dock panel.</param>
		public void AddScriptsToMenu(ActEditorWindow actEditor, Menu menu, DockPanel dockPanel) {
			lock (_lock) {
				_refreshMenu(menu);
				
				_actEditor = actEditor;
				_dockPanel = dockPanel;
				List<MenuItem> toAdd = new List<MenuItem>();
				ReloadLibraries();
				DeleteDlls();

				AlphanumComparer alphanumComparer = new AlphanumComparer(StringComparison.OrdinalIgnoreCase);
				
				foreach (string script in Directory.GetFiles(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath), "*.cs").OrderBy(p => p, alphanumComparer)) {
					try {
						if (Path.GetFileNameWithoutExtension(script) == "script_sample") continue;

						var hash = LibrariesConfiguration["[" + Path.GetFileName(script).GetHashCode() + "]", "NULL"];
						bool loadFromDll = false;
						string dllScript = script.ReplaceExtension(".dll");

						if (hash != "NULL") {
							if (File.Exists(dllScript)) {
								string md5Script = new Md5Hash().ComputeHash(File.ReadAllBytes(script));
								string md5Dll = new Md5Hash().ComputeHash(File.ReadAllBytes(dllScript));

								if (md5Script + "," + md5Dll == hash) {
									loadFromDll = true;
								}
							}
						}

						if (loadFromDll) {
							try {
								string localCopy = TemporaryFilesManager.GetTemporaryFilePath(_procId + "_script_{0:0000}");
								GrfPath.Delete(localCopy + ".dll");
								File.Copy(dllScript, localCopy + ".dll");
								File.Copy(script, localCopy + ".cs");

								_addScriptFromAssembly(localCopy + ".dll", toAdd);

								GrfPath.Delete(localCopy + ".cs");
							}
							catch {
								// Recompile if there's any error
								GrfPath.Delete(dllScript);

								string localCopy = TemporaryFilesManager.GetTemporaryFilePath(_procId + "_script_{0:0000}");
								GrfPath.Delete(localCopy + ".dll");
								File.Copy(script, localCopy + ".cs");
								_addFromScript(script, localCopy + ".cs", toAdd);
								GrfPath.Delete(localCopy + ".cs");
							}
						}
						else {
							string localCopy = TemporaryFilesManager.GetTemporaryFilePath(_procId + "_script_{0:0000}");
							GrfPath.Delete(script.ReplaceExtension(".dll"));
							File.Copy(script, localCopy + ".cs");
							_addFromScript(script, localCopy + ".cs", toAdd);
							GrfPath.Delete(localCopy + ".cs");
						}
					}
					catch (Exception err) {
						if (CombineErrors)
							PendingErrors.Add(err);
						else
							ErrorHandler.HandleException("Failed to load scripts.", err);
					}
				}

				_updateRedoUndoLeftMargin(_menu, dockPanel, toAdd);
			}
		}

		private void _refreshMenu(Menu menu) {
			if (_menu == null) {
				menu.Dispatch(delegate {
					foreach (MenuItem menuItem in menu.Items) {
						foreach (MenuItem item in menuItem.Items.OfType<MenuItem>()) {
							_initialMenuItems.Add(item);
						}
					}
				});
			}

			if (menu != null) {
				menu.Dispatch(delegate {
					for (int i = 0; i < menu.Items.Count; i++) {
						MenuItem menuItem = (MenuItem)menu.Items[i];

						for (int index = 0; index < menuItem.Items.Count; index++) {
							MenuItem mi = menuItem.Items[index] as MenuItem;

							if (mi == null) continue;

							if (!_initialMenuItems.Contains(mi)) {
								menuItem.Items.Remove(mi);
								index--;
							}
						}

						if (menuItem.Items.Count == 0) {
							menu.Items.Remove(menuItem);
							i--;
						}
					}
				});
			}

			_menu = menu;
		}

		public void DeleteDlls() {
			foreach (string dll in Directory.GetFiles(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath), "*.dll")) {
				if (!Libraries.Contains(Path.GetFileName(dll)) && !File.Exists(dll.ReplaceExtension(".cs"))) {
					GrfPath.Delete(dll);
				}
			}
		}

		/// <summary>
		/// Gets the script object from an assembly path.
		/// </summary>
		/// <param name="assemblyPath">The assembly path.</param>
		/// <returns></returns>
		public static IActScript GetScriptObjectFromAssembly(string assemblyPath) {
			Assembly assembly = Assembly.LoadFile(assemblyPath);
			object o = assembly.CreateInstance("Scripts.Script");

			if (o == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			IActScript actScript = o as IActScript;

			if (actScript == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			return actScript;
		}

		public static List<PortableExecutableReference> GetReferences() {
			LoadReferences();
			return _references;
		}

		private static List<PortableExecutableReference> _references;
		private static object _loadReferenceLock = new object();

		internal static void DummyCompile() {
			var scriptText = Encoding.Default.GetString(ApplicationManager.GetResource("dummy_script.cs"));
			var syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

			LoadReferences();

			var compilation = CSharpCompilation.Create(
				"DynamicAssembly",
				new[] { syntaxTree },
				_references,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
			
			EmitResult result = null;

			using (var ms = new MemoryStream()) {
				result = compilation.Emit(ms);
			}
		}

		private static void LoadReferences() {
			lock (_loadReferenceLock) {
				if (_references == null) {
					_references = new List<PortableExecutableReference>();

					var refs = AppDomain.CurrentDomain
						.GetAssemblies()
						.Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).ToList();

					foreach (var refName in Assembly.GetExecutingAssembly().GetReferencedAssemblies()) {
						if (refs.Any(a => a.GetName().FullName == refName.FullName))
							continue;

						try {
							var loaded = Assembly.Load(refName);
							if (!loaded.IsDynamic && !string.IsNullOrEmpty(loaded.Location)) {
								refs.Add(loaded);
							}
						}
						catch {
							// Some references might not resolve (satellite assemblies, etc.)
						}
					}

					foreach (var refAssembly in refs) {
						PortableExecutableReference metaReference;
						
						//if (File.Exists(refAssembly.Location.ReplaceExtension(".xml"))) {
						//	metaReference = MetadataReference.CreateFromFile(refAssembly.Location, documentation: XmlDocumentationProvider.CreateFromFile(refAssembly.Location.ReplaceExtension(".xml")));
						//}
						//else {
							metaReference = MetadataReference.CreateFromFile(refAssembly.Location);
						//}

						_references.Add(metaReference);
					}
				}
			}
		}

		/// <summary>
		/// Compiles the specified script file.
		/// </summary>
		/// <param name="scriptPath">The script.</param>
		/// <param name="dll">The path of the new DLL.</param>
		/// <returns>The result of the compilation</returns>
		internal static EmitResult Compile(string scriptPath, out string dll) {
			if (File.Exists(scriptPath.ReplaceExtension(".dll"))) {
				GrfPath.Delete(scriptPath.ReplaceExtension(".dll"));
			}

			var scriptText = File.ReadAllText(scriptPath);
			scriptText = scriptText.ReplaceAll("using GRF.System", "using GRF.GrfSystem");
			var syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

			LoadReferences();

			var compilation = CSharpCompilation.Create(
				"DynamicAssembly",
				new[] { syntaxTree },
				_references,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

			EmitResult result = null;

			using (var ms = new MemoryStream()) {
				result = compilation.Emit(ms);
				if (result.Success) {
					ms.Seek(0, SeekOrigin.Begin);
					File.WriteAllBytes(scriptPath.ReplaceExtension(".dll"), ms.ToArray());
				}
			}

			dll = scriptPath.ReplaceExtension(".dll");
			return result;
		}

		/// <summary>
		/// Verifies that example scripts are in the Scripts folder and that they are compiled. If not present, the scripts will be copied over.
		/// </summary>
		public static void VerifyExampleScriptsInstalled() {
			string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath);

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			try {
				Libraries.ToList().ForEach(v => Debug.Ignore(() => File.WriteAllBytes(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath, v), ApplicationManager.GetResource(v, true))));
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			foreach (string resource in ScriptNames) {
				bool modified = false;

				//foreach (string file in new string[] {resource + ".cs", resource + ".dll"}) {
				foreach (string file in new string[] {resource + ".cs"}) {
					string filePath = GrfPath.Combine(path, file);

					if (!File.Exists(filePath)) {
						File.WriteAllBytes(filePath, ApplicationManager.GetResource(file));
						modified = true;
					}
				}

				if (modified) {
					GrfPath.Delete(GrfPath.Combine(path, resource + ".dll"));
					LibrariesConfiguration["[" + Path.GetFileName(resource + ".cs").GetHashCode() + "]"] = "0";
				}
			}

			string[] resources = new string[] { "script7_add_effect1.act", "script7_add_effect1.spr", "script7_add_effect2.act", "script7_add_effect2.spr", "script7_add_effect3.act", "script7_add_effect3.spr" };

			foreach (string file in resources) {
				string filePath = GrfPath.Combine(path, file);

				if (!File.Exists(filePath)) {
					File.WriteAllBytes(filePath, ApplicationManager.GetResource(file));
				}
			}
		}

		private void _fileChanged(object sender, FileSystemEventArgs e) {
			try {
				// Raising events is turned off to avoid 
				// receiving the event twice.
				_fsw.EnableRaisingEvents = false;
				ReloadScripts();
			}
			finally {
				_fsw.EnableRaisingEvents = true;
			}
		}

		/// <summary>
		/// Setups the margin of the dock panel, for the Undo and Redo buttons.
		/// </summary>
		/// <param name="menu">The menu.</param>
		/// <param name="dockPanel">The dock panel.</param>
		private static void _updateRedoUndoLeftMargin(ItemsControl menu, FrameworkElement dockPanel) {
			if (dockPanel == null) return;

			double length = 0;
			var items = menu.Items.Cast<MenuItem>().ToList();

			if (items.Count > 0 && !items.Last().IsLoaded) {
				items.Last().Loaded += delegate {
					foreach (MenuItem mi in menu.Items) {
						length += mi.DesiredSize.Width;
					}

					dockPanel.Margin = new Thickness(length, 0, 0, 0);
				};
			}
			else {
				foreach (MenuItem mi in menu.Items) {
					length += mi.DesiredSize.Width;
				}

				dockPanel.Margin = new Thickness(length, 0, 0, 0);
			}
		}

		/// <summary>
		/// Setups the margin of the dock panel, for the Undo and Redo buttons.
		/// </summary>
		/// <param name="menu">The menu.</param>
		/// <param name="dockPanel">The dock panel.</param>
		/// <param name="toAdd">To add.</param>
		private static void _updateRedoUndoLeftMargin(ItemsControl menu, FrameworkElement dockPanel, ICollection<MenuItem> toAdd) {
			double length = 0;
			menu.Dispatch(delegate {
				foreach (var mi in toAdd) {
					menu.Items.Add(mi);
				}

				if (toAdd.Count > 0 && !toAdd.Last().IsLoaded) {
					toAdd.Last().Loaded += delegate {
						foreach (MenuItem mi in menu.Items) {
							length += mi.DesiredSize.Width;
						}

						dockPanel.Margin = new Thickness(Math.Ceiling(length), 0, 0, 0);
					};
				}
				else {
					foreach (MenuItem mi in menu.Items) {
						length += mi.DesiredSize.Width;
					}

					dockPanel.Margin = new Thickness(Math.Ceiling(length), 0, 0, 0);
				}
			});
		}

		/// <summary>
		/// Setups the menu item for both the menu and the script menu item.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="menu">The menu bar.</param>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="scriptMenu">The script menu.</param>
		/// <param name="toAdd">List of menu items to add in the menu.</param>
		private void _setupMenuItem(IActScript actScript, Menu menu, ActEditorWindow actEditor, UIElement scriptMenu, List<MenuItem> toAdd) {
			MenuItem menuItem = _retrieveConcernedMenuItem(actScript, menu, toAdd);

			menuItem.SubmenuOpened += delegate {
				int actionIndex = -1;
				int frameIndex = -1;
				int[] selectedLayers = new int[] {};

				var tab = actEditor.TabEngine.GetCurrentTab();

				if (tab == null) {
					return;
				}

				if (tab.Act != null) {
					actionIndex = tab._frameSelector.SelectedAction;
					frameIndex = tab._frameSelector.SelectedFrame;
					selectedLayers = tab.SelectionEngine.CurrentlySelected.OrderBy(p => p).ToArray();
				}

				scriptMenu.IsEnabled = actScript.CanExecute(tab.Act, actionIndex, frameIndex, selectedLayers);
			};

			string[] parameters = _getParameter(actScript, OverrideIndex);

			if (parameters != null && parameters.Length > 0) {
				int ival;

				if (Int32.TryParse(parameters[0], out ival)) {
					menuItem.Items.Insert(ival, scriptMenu);
				}
			}
			else {
				menuItem.Items.Add(scriptMenu);
			}
		}

		/// <summary>
		/// Retrieves the parameter from the DisplayName.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="parameter">The parameter to look for.</param>
		/// <returns>Returns the parameter if it's found; null otherwise.</returns>
		private static string[] _getParameter(IActScript actScript, string parameter) {
			string res = actScript.DisplayName as string;

			if (res == null) return null;

			int indexOfParam = res.IndexOf(parameter, 0, StringComparison.OrdinalIgnoreCase);

			if (indexOfParam > -1) {
				int indexOfEndParam = res.IndexOf("__", indexOfParam + parameter.Length, StringComparison.Ordinal);

				if (indexOfEndParam > -1) {
					string[] parameters = res.Substring(indexOfParam + parameter.Length, indexOfEndParam - (indexOfParam + parameter.Length)).Split(new string[] {","}, StringSplitOptions.RemoveEmptyEntries);

					return parameters;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the display string of a menu item's header.
		/// </summary>
		/// <param name="menuItem">The menu item.</param>
		/// <returns>Returns the string of a menu item's header.</returns>
		private static string _getString(HeaderedItemsControl menuItem) {
			Label header = menuItem.Header as Label;
			return header != null ? header.Content.ToString() : menuItem.Header.ToString();
		}

		/// <summary>
		/// Retrieves the menu item associated with the act script object's group property.
		/// If it is not found, it is automatically added.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <param name="menu">The menu.</param>
		/// <param name="toAdd">To add.</param>
		/// <returns></returns>
		private static MenuItem _retrieveConcernedMenuItem(IActScript actScript, Menu menu, List<MenuItem> toAdd) {
			if (actScript.Group.Contains("/") && toAdd == null) {
				// Script is requesting a submenu group
				string[] groups = actScript.Group.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

				List<MenuItem> menuItems = menu.Items.Cast<MenuItem>().ToList();
				MenuItem menuItem = null;
				ItemCollection collection = menu.Items;

				foreach (string group in groups) {
					menuItem = menuItems.FirstOrDefault(p => _getString(p) == group);

					if (menuItem == null) {
						if (group == groups[0])
							menuItem = new MenuItem {Header = new Label {Content = group, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-5, 0, -5, 0)}};
						else
							menuItem = new MenuItem {Header = group};

						collection.Add(menuItem);
					}

					menuItems = menuItem.Items.OfType<MenuItem>().ToList();
					collection = menuItem.Items;
				}

				return menuItem;
			}

			{
				MenuItem menuItem = menu.Items.Cast<MenuItem>().FirstOrDefault(p => _getString(p) == actScript.Group);

				if (toAdd != null) {
					if (menuItem == null) {
						menuItem = toAdd.FirstOrDefault(p => _getString(p) == actScript.Group);
					}
				}

				if (menuItem == null) {
					menuItem = new MenuItem {Header = new Label {Content = actScript.Group, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-5, 0, -5, 0)}};

					if (toAdd != null) {
						toAdd.Add(menuItem);
					}
					else {
						menu.Items.Add(menuItem);
					}
				}

				return menuItem;
			}
		}

		/// <summary>
		/// Generates the header displayed on the script menu.
		/// </summary>
		/// <param name="actScript">The act script.</param>
		/// <returns>The header of the menu item.</returns>
		internal static object GenerateHeader(IActScript actScript) {
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

		/// <summary>
		/// Generates the script menu from the act script's display name property.
		/// </summary>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="actScript">The act script.</param>
		/// <param name="isCompiledScript">Whether the script came from a compiled C# file or from runtime.</param>
		/// <returns>The menu item for the script's display name property.</returns>
		private MenuItem _generateScriptMenu(ActEditorWindow actEditor, IActScript actScript, bool isCompiledScript) {
			MenuItem scriptMenu = new MenuItem();

			scriptMenu.Header = GenerateHeader(actScript);

			if (actScript.InputGesture != null) {
				if (actScript.InputGesture.StartsWith("{")) {
					var gestureCmd = actScript.InputGesture.Trim('{', '}').Split('|');
					var gesture = ApplicationShortcut.GetGesture(gestureCmd[0]);

					if (gesture != null)
						scriptMenu.InputGestureText = gesture.IsAssigned ? ApplicationShortcut.FindDislayName(gesture) : "NA";
					else
						scriptMenu.InputGestureText = "NA";
				}
				else {
					scriptMenu.InputGestureText = actScript.InputGesture.Split(new char[] { ':' }).FirstOrDefault();
				}
			}
			if (actScript.Image != null) {
				scriptMenu.Icon = new Image { Source = GetImage(actScript.Image) };
				((Image)scriptMenu.Icon).SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
			}

			Action action = delegate {
				try {
					int actionIndex = -1;
					int frameIndex = -1;
					int[] selectedLayers = new int[] { };

					var tab = actEditor.TabEngine.GetCurrentTab();

					if (tab == null) {
						return;
					}

					if (tab.Act != null) {
						actionIndex = tab._frameSelector.SelectedAction;
						frameIndex = tab._frameSelector.SelectedFrame;
						selectedLayers = tab.SelectionEngine.CurrentlySelected.OrderBy(p => p).ToArray();
					}

					if (actScript.CanExecute(tab.Act, actionIndex, frameIndex, selectedLayers)) {
						int commandCount = -1;

						if (tab.Act != null) {
							commandCount = tab.Act.Commands.CommandIndex;
						}

						actScript.Execute(tab.Act, actionIndex, frameIndex, selectedLayers);

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
			};

			try {
				if (actScript.InputGesture != null) {
					if (actScript.InputGesture.StartsWith("{")) {
						scriptMenu.InputGestureText = "NA";

						var gestureCmd = actScript.InputGesture.Trim('{', '}').Split('|');

						if (gestureCmd.Length > 1) {
							scriptMenu.InputGestureText = gestureCmd[1];
						}

						scriptMenu.Loaded += delegate {
							var gesture = ApplicationShortcut.GetGesture(gestureCmd[0]);

							if (gesture == null) {
								ApplicationShortcut.Link(ApplicationShortcut.FromString(gestureCmd.Length > 1 ? gestureCmd[1] : "NULL", gestureCmd[0]), action, actEditor);
							}

							gesture = ApplicationShortcut.GetGesture(gestureCmd[0]);

							if (gesture == null) {
								scriptMenu.InputGestureText = "NA";
							}
							else {
								scriptMenu.InputGestureText = ApplicationShortcut.FindDislayNameMenuItem(gesture);
							}

							scriptMenu.InvalidateMeasure();
							scriptMenu.InvalidateArrange();
						};
					}
					else {
						foreach (var gesture in actScript.InputGesture.Split(':')) {
							ApplicationShortcut.Link(ApplicationShortcut.FromString(gesture, ((actScript.DisplayName is string) ? actScript.DisplayName.ToString() : gesture + "_cmd")), action, actEditor);
						}
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException("InputGesture field invalid for " + actScript.DisplayName, err);
			}

			scriptMenu.Click += (s, e) => action();

			try {
				if (isCompiledScript) {
					if (!_compiledScripts.ContainsKey(actScript.Group)) {
						_compiledScripts[actScript.Group] = new Dictionary<string, IActScript>();
					}

					_compiledScripts[actScript.Group][actScript.DisplayName.ToString()] = actScript;
				}
			}
			catch {
			}

			return scriptMenu;
		}

		private void _addFromScript(string script, string localCopy, List<MenuItem> toAdd) {
			try {
				string dll;
				var results = Compile(localCopy, out dll);

				if (!results.Success)
					throw new Exception(String.Join("\r\n", results.Diagnostics.ToList().Select(p => p.ToString()).ToArray()));

				LibrariesConfiguration["[" + Path.GetFileName(script).GetHashCode() + "]"] = new Md5Hash().ComputeHash(File.ReadAllBytes(script)) + "," + new Md5Hash().ComputeHash(File.ReadAllBytes(dll));

				GrfPath.Delete(localCopy);
				GrfPath.Delete(script.ReplaceExtension(".dll"));
				Debug.Ignore(() => File.Copy(dll, script.ReplaceExtension(".dll")));

				_addScriptFromAssembly(dll, toAdd);
			}
			catch (Exception err) {
				if (CombineErrors)
					PendingErrors.Add(new Exception("Failed to load: " + script, err));
				else
					ErrorHandler.HandleException("Failed to load: " + script, err);
			}
		}

		private void _addScriptFromAssembly(string assemblyPath, List<MenuItem> toAdd) {
			Assembly assembly = Assembly.LoadFile(assemblyPath);
			object o = assembly.CreateInstance("Scripts.Script");

			if (o == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			IActScript actScript = o as IActScript;

			if (actScript == null) throw new Exception("Couldn't instantiate the script object. Type not found?");

			_addActScript(actScript, toAdd);
		}

		private void _addActScript(IActScript actScript, List<MenuItem> toAdd) {
			_menu.Dispatch(() => _setupMenuItem(actScript, _menu, _actEditor, _generateScriptMenu(_actEditor, actScript, true), toAdd));
		}

		/// <summary>
		/// Retrieves the image from an image path (given by the act script object's properties).
		/// </summary>
		/// <param name="image">The image.</param>
		/// <returns></returns>
		public static ImageSource GetImage(string image) {
			var im = ApplicationManager.PreloadResourceImage(image);

			if (im != null) {
				return im;
			}

			var path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath, image);

			if (File.Exists(path)) {
				byte[] data = File.ReadAllBytes(path);
				GrfImage grfImage = new GrfImage(ref data);
				return grfImage.Cast<BitmapSource>();
			}

			return null;
		}

		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				if (_fsw != null) _fsw.Dispose();
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}