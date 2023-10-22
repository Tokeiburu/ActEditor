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

				GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.cs"));
				GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.dll"));
			}
			catch {
				return false;
			}

			return true;
		}
	}
}