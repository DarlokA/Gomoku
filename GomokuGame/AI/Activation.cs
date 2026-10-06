using System;

namespace GomokuGame.AI
{
    public enum ActivationType
    {
        ReLU,
        Sigmoid,
        Linear
    }

    public static class Activation
    {
        public static double Apply(ActivationType type, double x) => type switch
        {
            ActivationType.ReLU => Math.Max(0, x),
            ActivationType.Sigmoid => Sigmoid(x),
            ActivationType.Linear => x,
            _ => x
        };

        /// <summary>
        /// Производная функции активации по её выходу.
        /// Для ReLU — по pre-activation (z), для Sigmoid — по output (a).
        /// </summary>
        public static double Derivative(ActivationType type, double preActivation, double output) => type switch
        {
            ActivationType.ReLU => preActivation > 0 ? 1.0 : 0.0,
            ActivationType.Sigmoid => output * (1.0 - output),
            ActivationType.Linear => 1.0,
            _ => 1.0
        };

        private static double Sigmoid(double x)
        {
            // Численно устойчивый sigmoid
            if (x >= 0)
            {
                double e = Math.Exp(-x);
                return 1.0 / (1.0 + e);
            }
            else
            {
                double e = Math.Exp(x);
                return e / (1.0 + e);
            }
        }
    }
}