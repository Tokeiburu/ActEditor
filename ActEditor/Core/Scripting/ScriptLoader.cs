using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.IO;
using GRF.GrfSystem;
using TokeiLibrary;
using Utilities;
using Utilities.Extension;
using Utilities.Hash;
using Action = System.Action;
using Debug = Utilities.Debug;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using System.Text;
using ActEditor.Core.WPF.Dialogs;

namespace ActEditor.Core.Scripting {
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
		private object _lock = new object();
		private int _procId;
		private ActEditorWindow _actEditor;
		private DockPanel _dockPanel;
		private Menu _menu;
		private static Dictionary<string, Dictionary<string, IActScript>> _compiledScripts = new Dictionary<string, Dictionary<string, IActScript>>();
		public Dictionary<string, ActScriptLoader> _loadedActScripts = new Dictionary<string, ActScriptLoader>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptLoader" /> class.
		/// </summary>
		public ScriptLoader(Menu menu, FrameworkElement dockPanel) {
			_initializeFileWatcher();

			ApplicationManager.ThemeChanged += delegate {
				UpdateRedoUndoPosition(menu, dockPanel);
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

		private Debouncer _debouncer = new Debouncer();

		public void ShowErrors(ScriptLoaderResult result) {
			if (result.Errors.Count == 0) {
				_actEditor.Dispatch(delegate {
					if (ErrorDialog != null) {
						ErrorDialog.Close();
					}
				});
			}
			else {
				_actEditor.Dispatch(delegate {
					if (ErrorDialog == null) {
						ErrorDialog = new ScriptErrorDialog(result);
						ErrorDialog.Owner = WpfUtilities.TopWindow;
						ErrorDialog.Closed += delegate {
							ErrorDialog = null;
						};
						ErrorDialog.ShowDialog();
					}
					else {
						ErrorDialog.UpdateErrors(result);
					}
				});
			}
		}

		/// <summary>
		/// Reloads the scripts.
		/// </summary>
		public void ReloadScripts() {
			if (_actEditor == null || _menu == null) return;
			_compiledScripts.Clear();

			_debouncer.Execute(delegate {
				var result = ReloadScriptFolderToEditor(_actEditor, _menu, _dockPanel);

				ShowErrors(result);
			}, 100);
		}

		public void UnloadEditorScript(ActScriptLoader actScriptLoader) {
			var menuItem = actScriptLoader.MenuItem;

			_menu.Dispatch(delegate {
				while (menuItem != null) {
					if (menuItem.Parent is Menu menu) {
						menu.Items.Remove(menuItem);
						break;
					}
					else if (menuItem.Parent is MenuItem parent) {
						parent.Items.Remove(menuItem);

						if (parent.Items.Count != 0)
							break;

						menuItem = parent;
					}
					else {
						break;
					}
				}
			});
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
		public void AddScriptsToMenu(IActScript actScript, ActEditorWindow actEditor, Menu menu) {
			_actEditor = actEditor;
			_menu = menu;
			AddScriptToEditor(actScript);
		}

		/// <summary>
		/// Adds all the scripts (from the Scripts folder) to the menu.
		/// </summary>
		/// <param name="actEditor">The act editor.</param>
		/// <param name="menu">The menu.</param>
		/// <param name="dockPanel">The dock panel.</param>
		public ScriptLoaderResult ReloadScriptFolderToEditor(ActEditorWindow actEditor, Menu menu, DockPanel dockPanel) {
			lock (_lock) {
				_actEditor = actEditor;
				_dockPanel = dockPanel;

				ScriptLoaderResult scriptLoaderResult = new ScriptLoaderResult();
				AlphanumComparer alphanumComparer = new AlphanumComparer(StringComparison.OrdinalIgnoreCase);

				foreach (string script in Directory.GetFiles(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath), "*.cs").OrderBy(p => p, alphanumComparer)) {
					if (Path.GetFileNameWithoutExtension(script) == "script_sample") continue;

					var hash = LibrariesConfiguration["[" + Path.GetFileName(script).GetHashCode() + "]", "NULL"];
					bool loadFromDll = false;
					string dllPath = script.ReplaceExtension(".dll");

					if (hash != "NULL") {
						if (File.Exists(dllPath)) {
							string md5Script = new Md5Hash().ComputeHash(File.ReadAllBytes(script));
							string md5Dll = new Md5Hash().ComputeHash(File.ReadAllBytes(dllPath));

							if (_loadedActScripts.TryGetValue(script, out ActScriptLoader currentActScriptLoader)) {
								if (currentActScriptLoader.DllDataHash == md5Dll &&
									currentActScriptLoader.FileDataHash == md5Script) {
									continue;
								}
							}

							if (md5Script + "," + md5Dll == hash) {
								loadFromDll = true;
							}
						}
					}

					string tempCopy = TemporaryFilesManager.GetTemporaryFilePath(_procId + "_script_{0:0000}");
					string tempDllPath = tempCopy + ".dll";
					string tempScriptPath = tempCopy + ".cs";

					LoadScriptResult loadResult = null;

					if (loadFromDll) {
						GrfPath.Delete(tempDllPath);
						File.Copy(dllPath, tempDllPath);

						loadResult = LoadScriptFromAssembly(tempDllPath);
					}

					if (loadResult == null || !loadResult.Success) {
						GrfPath.Delete(tempDllPath);
						File.Copy(script, tempScriptPath);
						loadResult = LoadScriptFromCodeFile(tempScriptPath);

						if (loadResult.Success) {
							GrfPath.Delete(dllPath);
							File.Copy(tempDllPath, dllPath);
						}
					}

					loadResult.OriginalScriptPath = script;
					loadResult.OriginalDllPath = dllPath;

					if (loadResult.Success) {
						if (_loadedActScripts.TryGetValue(script, out ActScriptLoader currentActScriptLoader)) {
							UnloadEditorScript(currentActScriptLoader);
						}

						var actScriptLoader = AddScriptToEditor(loadResult.ActScript, scriptLoaderResult);
						actScriptLoader.FileNameHashCode = Path.GetFileName(script).GetHashCode();
						actScriptLoader.FileDataHash = new Md5Hash().ComputeHash(File.ReadAllBytes(script));
						actScriptLoader.DllDataHash = new Md5Hash().ComputeHash(File.ReadAllBytes(dllPath));

						LibrariesConfiguration["[" + actScriptLoader.FileNameHashCode + "]"] = actScriptLoader.FileDataHash + "," + actScriptLoader.DllDataHash;
						_loadedActScripts[script] = actScriptLoader;
					}
					else {
						scriptLoaderResult.Errors.Add(loadResult);
					}
				}

				AddEditorMenuItems(_menu, scriptLoaderResult.AddedMenuItems);
				UpdateRedoUndoPosition(_menu, _dockPanel);
				return scriptLoaderResult;
			}
		}

		public void DeleteDlls() {
			foreach (string dll in Directory.GetFiles(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, OutputPath), "*.dll")) {
				if (!Libraries.Contains(Path.GetFileName(dll)) && !File.Exists(dll.ReplaceExtension(".cs"))) {
					GrfPath.Delete(dll);
				}
			}
		}

		public static List<PortableExecutableReference> GetReferences() {
			LoadReferences();
			return _references;
		}

		private static List<PortableExecutableReference> _references;
		private static object _loadReferenceLock = new object();
		public ScriptErrorDialog ErrorDialog;

		internal static void DummyCompile() {
			CompileFromText(Encoding.Default.GetString(ApplicationManager.GetResource("dummy_script.cs")), null);
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
		public void UpdateRedoUndoPosition(ItemsControl menu, FrameworkElement dockPanel) {
			if (dockPanel == null) return;

			menu.Dispatch(delegate {
				var items = menu.Items.Cast<MenuItem>().ToList();

				menu.Dispatcher.BeginInvoke(new Action(() => {
					dockPanel.Margin = new Thickness(CalculateMenuWidth(menu), 0, 0, 0);
				}), System.Windows.Threading.DispatcherPriority.Background);
			});
		}

		public static double CalculateMenuWidth(ItemsControl menu) {
			double length = 0;

			foreach (MenuItem mi in menu.Items) {
				length += mi.DesiredSize.Width;
			}

			return Math.Ceiling(length);
		}

		public static void AddEditorMenuItems(ItemsControl menu, ICollection<MenuItem> toAdd) {
			menu.Dispatch(delegate {
				foreach (var mi in toAdd) {
					menu.Items.Add(mi);
				}
			});
		}

		public class ScriptCompileResult {
			public bool Success { get; set; }
			public EmitResult CompileResult { get; set; }
			public string DllOutput { get; set; }
		}

		public static ScriptCompileResult CompileFromFile(string path) {
			return CompileFromText(File.ReadAllText(path), path.ReplaceExtension(".dll"));
		}

		public static ScriptCompileResult CompileFromText(string scriptText, string dllOutput) {
			scriptText = scriptText.ReplaceAll("using GRF.System", "using GRF.GrfSystem");
			var syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

			ScriptCompileResult compileResult = new ScriptCompileResult();

			LoadReferences();

			var compilation = CSharpCompilation.Create(
				"DynamicAssembly",
				new[] { syntaxTree },
				_references,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

			EmitResult result = null;

			using (var ms = new MemoryStream()) {
				result = compilation.Emit(ms);
				if (result.Success && dllOutput != null) {
					ms.Seek(0, SeekOrigin.Begin);
					File.WriteAllBytes(dllOutput, ms.ToArray());
				}
			}

			compileResult.Success = result.Success;
			compileResult.CompileResult = result;
			compileResult.DllOutput = dllOutput;
			return compileResult;
		}

		

		public static LoadScriptResult LoadScriptFromAssembly(string assemblyPath) {
			Assembly assembly = Assembly.LoadFile(assemblyPath);
			object o = assembly.CreateInstance("Scripts.Script");

			if (o == null)
				return LoadScriptResult.Fail("Couldn't instantiate the script object. Type not found?", assemblyPath);

			IActScript actScript = o as IActScript;

			if (actScript == null)
				return LoadScriptResult.Fail("Couldn't instantiate the script object. Type not found?", assemblyPath);

			LoadScriptResult result = new LoadScriptResult();
			result.Success = true;
			result.ActScript = actScript;
			return result;
		}

		public ActScriptLoader AddScriptToEditor(IActScript actScript, ScriptLoaderResult scriptLoader = null) {
			return _menu.Dispatch(delegate {
				ActScriptLoader loader = new ActScriptLoader(actScript, _actEditor);
				AddCompiledScript(actScript);
				loader.AddToEditor(_menu, scriptLoader);
				return loader;
			});
		}

		public void AddCompiledScript(IActScript actScript) {
			if (!_compiledScripts.ContainsKey(actScript.Group)) {
				_compiledScripts[actScript.Group] = new Dictionary<string, IActScript>();
			}

			_compiledScripts[actScript.Group][actScript.DisplayName.ToString()] = actScript;
		}

		public static LoadScriptResult LoadScriptFromCodeFile(string path) {
			LoadScriptResult result = new LoadScriptResult();
			var compileResult = CompileFromFile(path);

			if (!compileResult.Success) {
				return LoadScriptResult.Fail(compileResult.CompileResult, path);
			}

			return LoadScriptFromAssembly(compileResult.DllOutput);
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