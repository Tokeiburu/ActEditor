using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using TokeiLibrary;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public class FadeParticleEffect : ImageProcessingEffect {
		public override string Image => "empty.png";
		public override string Group => @"Effects/Global";

		public class EffectOptions {
			public TkVector2 StartOffset;
			public int SpawnWidth;
			public int SpawnHeight;
			public int RepeatCount;
			public Func<float, float> FadeEase;
			public TkVector2 Direction;
			public int ParticleCount;
			public GrfColor ParticleColor;
			public float MinScale;
			public float MaxScale;
			public int ZMode;
			public Random Rng = new Random(1234);
		}

		private EffectOptions _options = new EffectOptions();

		public FadeParticleEffect() : base("Fade particle") {
			_inputGesture = "{Dialog.AnimationFadeParticle}";
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("StartOffset", new TkVector2(0, 0), new TkVector2(-100, 100), new TkVector2(-100, 100));
			effect.AddProperty("SpawnWidth", 100, 0, 200);
			effect.AddProperty("SpawnHeight", 100, 0, 200);
			//effect.AddProperty("RepeatCount", 1, 1, 5);
			effect.AddProperty("FadeEase", 0, -50, 50);
			effect.AddProperty("Direction", new TkVector2(0, 40), new TkVector2(-100, 100), new TkVector2(-100, 100));
			effect.AddProperty("ParticleCount", 10, 1, 100);
			effect.AddProperty("ParticleColor", new GrfColor(255, 0, 0, 0), default, default);
			effect.AddProperty("MinScale", 0.1f, 0f, 2f);
			effect.AddProperty("MaxScale", 0.3f, 0f, 2f);
			effect.AddProperty("ZMode", 2, 0, 2);
			effect.AddProperty("RngSeed", 1234, 0, 10000);

			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.SetEditType(AnimationEditTypes.TargetOnly);
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);

			_options.StartOffset = effect.GetProperty<TkVector2>("StartOffset");
			_options.SpawnWidth = effect.GetProperty<int>("SpawnWidth");
			_options.SpawnHeight = effect.GetProperty<int>("SpawnHeight");
			//_options.RepeatCount = effect.GetProperty<int>("RepeatCount");
			_options.FadeEase = InterpolationAnimation.GetEaseMethod(effect.GetProperty<int>("FadeEase"));
			_options.Direction = effect.GetProperty<TkVector2>("Direction");
			_options.ParticleCount = effect.GetProperty<int>("ParticleCount");
			_options.ParticleColor = effect.GetProperty<GrfColor>("ParticleColor");
			_options.MinScale = effect.GetProperty<float>("MinScale");
			_options.MaxScale = effect.GetProperty<float>("MaxScale");
			_options.ZMode = effect.GetProperty<int>("ZMode");
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));

			_options.StartOffset = new TkVector2(_options.StartOffset.X, -_options.StartOffset.Y);
			_options.Direction = new TkVector2(_options.Direction.X, -_options.Direction.Y);
		}

		public override void OnBackupCommand(EffectConfiguration effect) {
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));
			_particles.Clear();

			for (int i = 0; i < _options.ParticleCount; i++) {
				Particle p = new Particle();

				p.StartX = (int)(_options.Rng.Next(_options.SpawnWidth) - _options.SpawnWidth / 2d + _options.StartOffset.X);
				p.StartY = (int)(-_options.Rng.Next(_options.SpawnHeight) + _options.StartOffset.Y);
				p.Time = (float)_options.Rng.NextDouble();
				p.Scale = (float)(_options.Rng.NextDouble() * (_options.MaxScale - _options.MinScale) + _options.MinScale);

				if (_options.ZMode == 0) {
					p.InsertIndex = int.MaxValue;
				}
				else if (_options.ZMode == 1) {
					p.InsertIndex = int.MinValue;
				}
				else {
					p.InsertIndex = i < _options.ParticleCount / 2 ? int.MaxValue : int.MinValue;
				}

				_particles.Add(p);
			}

			_spriteIndex = _actInput.Sprite.InsertAny(new GrfImage(ApplicationManager.GetResource("particle.png")));
		}

		public struct Particle {
			public int StartX;
			public int StartY;
			public float Time;
			public float Scale;
			public int InsertIndex;
		};

		private List<Particle> _particles = new List<Particle>();
		private SpriteIndex _spriteIndex;

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animLength = action.NumberOfFrames;
			float bMult = 1f / (action.NumberOfFrames - 1f);

			for (int i = 0; i < action.Frames.Count; i++) {
				Frame frame = action[i];

				List<Layer> insertBack = new List<Layer>();
				List<Layer> insertFront = new List<Layer>();

				for (int k = 0; k < _particles.Count; k++) {
					Layer layer = new Layer(_spriteIndex);
					Particle particle = _particles[k];
					float t = (float)(Math.Round(particle.Time * action.NumberOfFrames) / (action.NumberOfFrames - 1f)) + i * bMult;

					if (t > 1.0f)
						t -= 1.0f + bMult;

					float tEase = _options.FadeEase(t);

					layer.Color = new GrfColor((byte)((1 - tEase) * _options.ParticleColor.A), _options.ParticleColor.R, _options.ParticleColor.G, _options.ParticleColor.B);

					layer.OffsetX = (int)(particle.StartX + tEase * _options.Direction.X);
					layer.OffsetY = (int)(particle.StartY + tEase * _options.Direction.Y);

					if (_status.Aid % 8 >= 4)
						layer.OffsetX *= -1;

					layer.ScaleX = particle.Scale;
					layer.ScaleY = particle.Scale;

					if (particle.InsertIndex < 0)
						insertBack.Add(layer);
					else
						insertFront.Add(layer);
				}

				foreach (var layer in insertBack) {
					frame.Layers.Insert(0, layer);
				}

				foreach (var layer in insertFront) {
					frame.Layers.Add(layer);
				}
			}
		}
	}
}
