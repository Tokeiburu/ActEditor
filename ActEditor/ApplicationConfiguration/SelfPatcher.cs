using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ActEditor.Core;
using GRF.IO;

namespace ActEditor.ApplicationConfiguration {
	/// <summary>
	/// Class imported from GrfEditor
	/// </summary>
	public class SelfPatcher {
		public static List<SelfPatch> Patches = new List<SelfPatch>();

		public static readonly SelfPatch Patch0013 = new RefreshScripts(13);
		public static readonly SelfPatch Patch0016 = new RefreshScripts(16);
		public static readonly SelfPatch Patch0024 = new RefreshScripts2(24);
		public static readonly SelfPatch Patch0125 = new RefreshScripts2(125);
		public static readonly SelfPatch Patch0126 = new RefreshScripts3(126);
		public static readonly SelfPatch Patch0127 = new RefreshScripts4(127);
		public static readonly SelfPatch Patch0128 = new RefreshScripts5(128);
		public static readonly SelfPatch Patch0131 = new RefreshScripts6(131);
		public static readonly SelfPatch Patch0132 = new RefreshScripts7(132);
		public static readonly SelfPatch Patch0133 = new RefreshScripts8(133);
		public static readonly SelfPatch Patch0134 = new RefreshScripts9(134);

		static SelfPatcher() {
			Patches = Patches.OrderBy(p => p.PatchId).ToList();
		}

		public static void SelfPatch() {
			int currentPatchId = ActEditorConfiguration.PatchId;

			foreach (SelfPatch patch in Patches) {
				if (patch.PatchId >= currentPatchId) {
					patch.PatchAppliaction();
					currentPatchId = patch.PatchId + 1;
				}
			}

			ActEditorConfiguration.PatchId = currentPatchId;
		}
	}

	public abstract class SelfPatch {
		private readonly int _patchId;

		protected SelfPatch(int patchId) {
			_patchId = patchId;

			SelfPatcher.Patches.Add(this);
		}

		public int PatchId {
			get { return _patchId; }
		}

		public abstract bool PatchAppliaction();

		public void Safe(Action action) {
			try {
				action();
			}
			catch {
			}
		}
	}

	public class RefreshScripts : SelfPatch {
		public RefreshScripts(int patchId) : base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				foreach (string resource in ScriptLoader.ScriptNames) {
					foreach (string file in new string[] {resource + ".cs", resource + ".dll"}) {
						GrfPath.Delete(Path.Combine(path, file));
					}
				}
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts2 : SelfPatch {
		public RefreshScripts2(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				foreach (string resource in ScriptLoader.ScriptNames) {
					foreach (string file in new string[] { resource + ".cs", resource + ".dll" }) {
						GrfPath.Delete(Path.Combine(path, file));
					}
				}

				GrfPath.Delete(Path.Combine(path, "script3_mirror_frame.cs"));
				GrfPath.Delete(Path.Combine(path, "script3_mirror_frame.dll"));
				GrfPath.Delete(Path.Combine(path, "script0_magnifyAll.cs"));
				GrfPath.Delete(Path.Combine(path, "script0_magnifyAll.dll"));
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts3 : SelfPatch {
		public RefreshScripts3(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				GrfPath.Delete(Path.Combine(path, "script2_expand.cs"));
				GrfPath.Delete(Path.Combine(path, "script2_expand.dll"));

				GrfPath.Delete(Path.Combine(path, "script7_add_effect1.cs"));
				GrfPath.Delete(Path.Combine(path, "script7_add_effect1.dll"));

				GrfPath.Delete(Path.Combine(path, "script8_add_frames.cs"));
				GrfPath.Delete(Path.Combine(path, "script8_add_frames.dll"));

				GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.cs"));
				GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.dll"));
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts4 : SelfPatch {
		public RefreshScripts4(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				GrfPath.Delete(Path.Combine(path, "script0_magnify.cs"));
				GrfPath.Delete(Path.Combine(path, "script0_magnify.dll"));
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts5 : SelfPatch {
		public RefreshScripts5(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				GrfPath.Delete(Path.Combine(path, "script10_trim_images.cs"));
				GrfPath.Delete(Path.Combine(path, "script10_trim_images.dll"));
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts6 : SelfPatch {
		public RefreshScripts6(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.cs"));
				GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.dll"));

				GrfPath.Delete(Path.Combine(path, "script10_trim_images.cs"));
				GrfPath.Delete(Path.Combine(path, "script10_trim_images.dll"));

				GrfPath.Delete(Path.Combine(path, "script11_palette_sheet.cs"));
				GrfPath.Delete(Path.Combine(path, "script11_palette_sheet.dll"));
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts7 : SelfPatch {
		public RefreshScripts7(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				GrfPath.Delete(Path.Combine(path, "script6_merge_layers.cs"));
				GrfPath.Delete(Path.Combine(path, "script6_merge_layers.dll"));
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts8 : SelfPatch {
		public RefreshScripts8(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				GrfPath.Delete(Path.Combine(path, "script0_magnify.cs"));
				GrfPath.Delete(Path.Combine(path, "script0_magnify.dll"));

				GrfPath.Delete(Path.Combine(path, "script2_expand.cs"));
				GrfPath.Delete(Path.Combine(path, "script2_expand.dll"));

				GrfPath.Delete(Path.Combine(path, "script7_add_effect1.cs"));
				GrfPath.Delete(Path.Combine(path, "script7_add_effect1.dll"));

				GrfPath.Delete(Path.Combine(path, "script8_add_frame.cs"));
				GrfPath.Delete(Path.Combine(path, "script8_add_frame.dll"));

				GrfPath.Delete(Path.Combine(path, "script10_trim_images.cs"));
				GrfPath.Delete(Path.Combine(path, "script10_trim_images.dll"));

				GrfPath.Delete(Path.Combine(path, "sprites.conf"));
				GrfPath.Delete(Path.Combine(path, "sprites_old.conf"));
			}
			catch {
				return false;
			}

			return true;
		}
	}

	public class RefreshScripts9 : SelfPatch {
		public RefreshScripts9(int patchId)
			: base(patchId) {
		}

		public override bool PatchAppliaction() {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				foreach (var dllPath in Directory.GetFiles(path, "*.dll")) {
					GrfPath.Delete(dllPath);
				}

				GrfPath.Delete(Path.Combine(path, "script7_add_effect1.cs"));
				GrfPath.Delete(Path.Combine(path, "script8_add_frames.cs"));
			}
			catch {
				return false;
			}

			return true;
		}
	}
}