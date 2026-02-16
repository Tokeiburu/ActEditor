using System;
using System.Collections.Generic;
using System.Linq;
using ActEditor.Core.DrawingComponents;

namespace ActEditor.Core.WPF.FrameEditor {
	public class DefaultDrawModule : IDrawingModule {
		private readonly Func<List<DrawingComponent>> _getComponents;
		private readonly DrawingPriorityValues _priority;
		private readonly bool _permanent;

		public int DrawingPriority {
			get { return (int)_priority; }
		}

		public List<DrawingComponent> GetComponents() {
			return _getComponents() ?? new List<DrawingComponent>();
		}

		public bool Permanent {
			get { return _permanent; }
		}

		public DefaultDrawModule(Func<List<DrawingComponent>> getComponents, DrawingPriorityValues priority, bool permanent) {
			_getComponents = getComponents;
			_priority = priority;
			_permanent = permanent;
		}
	}

	public class ReferenceDrawModule : IDrawingModule {
		private readonly IFrameRendererEditor _editor;
		private readonly DrawingPriorityValues _priority;
		private readonly bool _permanent;
		private List<DrawingComponent> _cached;

		public int DrawingPriority {
			get { return (int)_priority; }
		}

		public List<DrawingComponent> GetComponents() {
			return _cached;
		}

		public bool Permanent {
			get { return _permanent; }
		}

		public ReferenceDrawModule(IFrameRendererEditor editor, DrawingPriorityValues priority, bool permanent) {
			_editor = editor;
			_priority = priority;
			_permanent = permanent;
			
			editor.ReferencesChanged += delegate {
				_generateComponents();
				editor.FrameRenderer.Update();
			};

			_generateComponents();
		}

		private void _generateComponents() {
			_cached = _editor.References.Where(p => p.ShowReference && p.Mode == (_priority == DrawingPriorityValues.Back ? ZMode.Back : ZMode.Front)).Select(p => (DrawingComponent)new ActDraw(p.Act, _editor)).ToList();
			//Console.WriteLine("Updated ReferenceDrawModule components");
		}
	}

	public class BufferedDrawModule : IDrawingModule {
		private readonly Func<(bool, List<DrawingComponent>)> _getComponents;
		private readonly DrawingPriorityValues _priority;
		private readonly bool _permanent;
		private List<DrawingComponent> _cached;

		public int DrawingPriority {
			get { return (int)_priority; }
		}

		public List<DrawingComponent> GetComponents() {
			//return _getComponents().Item2 ?? new List<DrawingComponent>();

			if (_cached != null)
				return _cached;
			
			var result = _getComponents();
			
			if (!result.Item1) {
				return result.Item2 ?? new List<DrawingComponent>();
			}
			
			_cached = result.Item2;
			return _cached;
		}

		public bool Permanent {
			get { return _permanent; }
		}

		public BufferedDrawModule(Func<(bool, List<DrawingComponent>)> getComponents, DrawingPriorityValues priority, bool permanent) {
			_getComponents = getComponents;
			_priority = priority;
			_permanent = permanent;
		}
	}
}
