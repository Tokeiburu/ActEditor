using System;
using System.Collections.Generic;
using System.IO;
using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using TokeiLibrary;
using TokeiLibrary.Paths;
using Utilities.Extension;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts {
	public class EditAnchor : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			ActEditorWindow.Instance.GetCurrentTab2()._rendererPrimary.AnchorModule.EditAnchors();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act[selectedActionIndex, selectedFrameIndex].NumberOfLayers > 0;
		}

		public object DisplayName {
			get { return "Edit frame anchor position"; }
		}

		public string Group {
			get { return "Anchors"; }
		}

		public string InputGesture {
			get { return "{FrameEditor.EditAnchorPosition|Ctrl-K}"; }
		}

		public string Image {
			get { return "anchor.png"; }
		}

		#endregion
	}

	public class ImportAnchor : IActScript {
		public ActEditorWindow ActEditor { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				string path = TkPathRequest.OpenFile<ActEditorConfiguration>("AppLastPath", "filter", FileFormat.MergeFilters(Format.Act));

				if (path == null) return;

				if (!path.IsExtension(".act"))
					throw new Exception("Invalid file extension (act file supported only).");

				Act loadedAct = new Act(File.ReadAllBytes(path), new Spr());

				act.Commands.Begin();
				act.Commands.Backup(_ => {
					for (int i = 0; i < act.NumberOfActions && i < loadedAct.NumberOfActions; i++) {
						if (0 <= i && i < 8 ||
						    16 <= i && i < 24) {
							Action action = act[i];
							Action loadedAction = loadedAct[i];

							for (int j = 0; j < action.NumberOfFrames; j++) {
								Frame frame = act[i, j];
								frame.Anchors.Clear();

								if (loadedAction.NumberOfFrames == 0)
									continue;

								int expectedIndex = (int) (((float) j / action.NumberOfFrames) * loadedAction.NumberOfFrames);

								frame.Anchors.AddRange(loadedAction[expectedIndex].Anchors);
							}
						}
						else {
							for (int j = 0; j < act[i].NumberOfFrames; j++) {
								Frame frame = act[i, j];
								frame.Anchors.Clear();

								Frame frameRef = loadedAct.TryGetFrame(i, j);

								if (frameRef == null) {
									frameRef = loadedAct.TryGetFrame(i, loadedAct[i].NumberOfFrames - 1);

									if (frameRef == null) {
										continue;
									}
								}

								frame.Anchors.AddRange(frameRef.Anchors);
							}
						}
					}
				}, "Import anchors", true);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Set from file..."; }
		}

		public string Group {
			get { return "Anchors/Set anchors"; }
		}

		public string InputGesture {
			get { return "{Dialog.ImportAnchor|Ctrl-Alt-K}"; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}

	public class ImportDefaultMaleAnchor : IActScript {
		public ActEditorWindow ActEditor { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				Act loadedAct = new Act(ApplicationManager.GetResource("ref_head_m.act"), new Spr());

				act.Commands.Begin();
				act.Commands.Backup(_ => {
					for (int i = 0; i < act.NumberOfActions && i < loadedAct.NumberOfActions; i++) {
						if (0 <= i && i < 8 ||
						    16 <= i && i < 24) {
							Action action = act[i];
							Action loadedAction = loadedAct[i];

							for (int j = 0; j < action.NumberOfFrames; j++) {
								Frame frame = act[i, j];
								frame.Anchors.Clear();

								if (loadedAction.NumberOfFrames == 0)
									continue;

								int expectedIndex = (int) (((float) j / action.NumberOfFrames) * loadedAction.NumberOfFrames);

								frame.Anchors.AddRange(loadedAction[expectedIndex].Anchors);
							}
						}
						else {
							for (int j = 0; j < act[i].NumberOfFrames; j++) {
								Frame frame = act[i, j];
								frame.Anchors.Clear();

								Frame frameRef = loadedAct.TryGetFrame(i, j);

								if (frameRef == null) {
									frameRef = loadedAct.TryGetFrame(i, loadedAct[i].NumberOfFrames - 1);

									if (frameRef == null) {
										continue;
									}
								}

								frame.Anchors.AddRange(frameRef.Anchors);
							}
						}
					}
				}, "Import default anchors (male)", true);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Set default (male)"; }
		}

		public string Group {
			get { return "Anchors/Set anchors"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}

	public class ImportDefaultFemaleAnchor : IActScript {
		public ActEditorWindow ActEditor { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				Act loadedAct = new Act(ApplicationManager.GetResource("ref_head_f.act"), new Spr());

				act.Commands.Begin();
				act.Commands.Backup(_ => {
					for (int i = 0; i < act.NumberOfActions && i < loadedAct.NumberOfActions; i++) {
						if (0 <= i && i < 8 ||
						    16 <= i && i < 24) {
							Action action = act[i];
							Action loadedAction = loadedAct[i];

							for (int j = 0; j < action.NumberOfFrames; j++) {
								Frame frame = act[i, j];
								frame.Anchors.Clear();

								if (loadedAction.NumberOfFrames == 0)
									continue;

								int expectedIndex = (int) (((float) j / action.NumberOfFrames) * loadedAction.NumberOfFrames);

								frame.Anchors.AddRange(loadedAction[expectedIndex].Anchors);
							}
						}
						else {
							for (int j = 0; j < act[i].NumberOfFrames; j++) {
								Frame frame = act[i, j];
								frame.Anchors.Clear();

								Frame frameRef = loadedAct.TryGetFrame(i, j);

								if (frameRef == null) {
									frameRef = loadedAct.TryGetFrame(i, loadedAct[i].NumberOfFrames - 1);

									if (frameRef == null) {
										continue;
									}
								}

								frame.Anchors.AddRange(frameRef.Anchors);
							}
						}
					}
				}, "Import default anchors (female)", true);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Set default (female)"; }
		}

		public string Group {
			get { return "Anchors/Set anchors"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}

	public class AdjustAnchor : IActScript {
		public ActEditorWindow ActEditor { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				string path = TkPathRequest.OpenFile<ActEditorConfiguration>("AppLastPath", "filter", FileFormat.MergeFilters(Format.Act));

				if (path == null) return;

				if (!path.IsExtension(".act"))
					throw new Exception("Invalid file extension (act file supported only).");

				AdjustAnchors(act, new Act(File.ReadAllBytes(path), new Spr()));
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Adjust from file..."; }
		}

		public string Group {
			get { return "Anchors/Adjust anchors"; }
		}

		public string InputGesture {
			get { return "{Dialog.AdjustAnchor}"; }
		}

		public string Image {
			get { return null; }
		}

		#endregion

		public static void AdjustAnchors(Act act, Act loadedAct) {
			act.Commands.Begin();
			act.Commands.Backup(_ => {
				for (int i = 0; i < act.NumberOfActions && i < loadedAct.NumberOfActions; i++) {
					if (0 <= i && i < 8 ||
					    16 <= i && i < 24) {
						Action action = act[i];
						Action loadedAction = loadedAct[i];

						for (int j = 0; j < action.NumberOfFrames; j++) {
							Frame frame = act[i, j];

							if (loadedAction.NumberOfFrames == 0) {
								frame.Anchors.Clear();
								continue;
							}

							int expectedIndex = (int) (((float) j / action.NumberOfFrames) * loadedAction.NumberOfFrames);
							List<Anchor> expectedAnchors = loadedAction[expectedIndex].Anchors;

							_adjust(frame, frame.Anchors, expectedAnchors);
						}
					}
					else {
						for (int j = 0; j < act[i].NumberOfFrames; j++) {
							Frame frame = act[i, j];
							Frame frameRef = loadedAct.TryGetFrame(i, j);


							if (frameRef == null) {
								frameRef = loadedAct.TryGetFrame(i, loadedAct[i].NumberOfFrames - 1);

								if (frameRef == null) {
									frame.Anchors.Clear();
									continue;
								}
							}

							_adjust(frame, frame.Anchors, frameRef.Anchors);
						}
					}
				}
			}, "Adjust anchors", true);
		}

		private static void _adjust(IEnumerable<Layer> frame, List<Anchor> anchors, List<Anchor> refAnchors) {
			if (anchors.Count == 0 && refAnchors.Count == 0)
				return;

			if (refAnchors.Count > 0 && anchors.Count == 0) {
				anchors.Clear();
				return;
				//anchors.Add(new Anchor(refAnchors[0]));
				//anchors[0].OffsetX = 0;
				//anchors[0].OffsetY = 0;
			}

			if (refAnchors.Count == 0 && anchors.Count > 0) {
				anchors.Clear();
				return;
				//refAnchors.Add(new Anchor(anchors[0]));
				//refAnchors[0].OffsetX = 0;
				//refAnchors[0].OffsetY = 0;
			}


			if (anchors.Count > 0 && refAnchors.Count > 0) {
				int diffX = 0;
				int diffY = 0;

				diffX = refAnchors[0].OffsetX - anchors[0].OffsetX;
				diffY = refAnchors[0].OffsetY - anchors[0].OffsetY;

				anchors[0] = refAnchors[0];

				foreach (Layer layer in frame) {
					layer.OffsetX += diffX;
					layer.OffsetY += diffY;
				}
			}
		}
	}

	public class AdjustAnchorMale : IActScript {
		public ActEditorWindow ActEditor { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				AdjustAnchor.AdjustAnchors(act, new Act(ApplicationManager.GetResource("ref_head_m.act"), new Spr()));
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Adjust anchors (male)"; }
		}

		public string Group {
			get { return "Anchors/Adjust anchors"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}

	public class AdjustAnchorFemale : IActScript {
		public ActEditorWindow ActEditor { get; set; }

		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				AdjustAnchor.AdjustAnchors(act, new Act(ApplicationManager.GetResource("ref_head_f.act"), new Spr()));
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Adjust anchors (female)"; }
		}

		public string Group {
			get { return "Anchors/Adjust anchors"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}
}