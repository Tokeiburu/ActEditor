using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ActEditor.Core.WPF.FrameEditor {
	public class ColorMultiplyEffect : ShaderEffect {
		private static readonly PixelShader _shader = new PixelShader {
			UriSource = new Uri("pack://application:,,,/Resources/Shaders/ColorMultiply.ps")
		};

		public ColorMultiplyEffect() {
			PixelShader = _shader;
			UpdateShaderValue(InputProperty);
			UpdateShaderValue(ColorProperty);
		}

		public static readonly DependencyProperty InputProperty =
			RegisterPixelShaderSamplerProperty(
				"Input", typeof(ColorMultiplyEffect), 0);

		public static readonly DependencyProperty ColorProperty =
			DependencyProperty.Register(
				"Color", typeof(Color), typeof(ColorMultiplyEffect),
				new UIPropertyMetadata(Colors.White,
					PixelShaderConstantCallback(0)));

		public Color Color {
			get => (Color)GetValue(ColorProperty);
			set => SetValue(ColorProperty, value);
		}
	}
}
