using UnityEngine;
using UnityEngine.UI;

namespace FaseLucasGame
{
    public enum NodeKind
    {
        // Sensors (outputs only)
        PosX, PosY, PosZ, BelowColor, BelowDistance, IsHolding,
        // Score sensors (outputs only) - how many of each colour are already burned + the goal
        RedScore, BlueScore, Goal,
        // Values / variables
        Constant, VarGet, VarSet,
        // Math (variable changes)
        Add, Sub, Mul,
        // Comparisons
        Greater, Less, Equals,
        // Logic gates
        And, Or, Not,
        // Actions (sinks) - XYZ controls + grab
        MoveX, MoveY, MoveZ, Grab, Release,
        // Smart blocks (easy mode) - high level helpers that bundle a lot of behaviour
        NeedBelow, Search, Deliver
    }

    /// <summary>Static description of how each node kind behaves.</summary>
    public struct NodeSpec
    {
        public string label;
        public int inputCount;
        public string[] inputLabels;
        public bool hasOutput;
        public bool isSink;
        public bool hasValueField;
        public bool hasVarField;
        public Color color;

        public static NodeSpec Get(NodeKind k)
        {
            Color sensor = new Color(0.16f, 0.32f, 0.42f, 1f);
            Color value = new Color(0.30f, 0.28f, 0.16f, 1f);
            Color math = new Color(0.20f, 0.30f, 0.20f, 1f);
            Color logic = new Color(0.32f, 0.22f, 0.34f, 1f);
            Color action = new Color(0.40f, 0.18f, 0.18f, 1f);
            Color smart = new Color(0.20f, 0.36f, 0.30f, 1f);   // high-level "easy mode" helpers

            switch (k)
            {
                case NodeKind.PosX: return New("Pos X", 0, null, true, false, false, false, sensor);
                case NodeKind.PosY: return New("Pos Y", 0, null, true, false, false, false, sensor);
                case NodeKind.PosZ: return New("Pos Z", 0, null, true, false, false, false, sensor);
                case NodeKind.BelowColor: return New("Cor Abaixo (1=Verm 2=Azul)", 0, null, true, false, false, false, sensor);
                case NodeKind.BelowDistance: return New("Dist. Abaixo", 0, null, true, false, false, false, sensor);
                case NodeKind.IsHolding: return New("Segurando?", 0, null, true, false, false, false, sensor);

                case NodeKind.RedScore: return New("Vermelhos (pontos)", 0, null, true, false, false, false, sensor);
                case NodeKind.BlueScore: return New("Azuis (pontos)", 0, null, true, false, false, false, sensor);
                case NodeKind.Goal: return New("Meta por cor", 0, null, true, false, false, false, sensor);

                case NodeKind.Constant: return New("Numero", 0, null, true, false, true, false, value);
                case NodeKind.VarGet: return New("Ler Variavel", 0, null, true, false, false, true, value);
                case NodeKind.VarSet: return New("Definir Variavel", 1, new[] { "valor" }, true, true, false, true, value);

                case NodeKind.Add: return New("Somar (+)", 2, new[] { "a", "b" }, true, false, false, false, math);
                case NodeKind.Sub: return New("Subtrair (-)", 2, new[] { "a", "b" }, true, false, false, false, math);
                case NodeKind.Mul: return New("Multiplicar (x)", 2, new[] { "a", "b" }, true, false, false, false, math);

                case NodeKind.Greater: return New("Maior (>)", 2, new[] { "a", "b" }, true, false, false, false, logic);
                case NodeKind.Less: return New("Menor (<)", 2, new[] { "a", "b" }, true, false, false, false, logic);
                case NodeKind.Equals: return New("Igual (=)", 2, new[] { "a", "b" }, true, false, false, false, logic);

                case NodeKind.And: return New("E (AND)", 2, new[] { "a", "b" }, true, false, false, false, logic);
                case NodeKind.Or: return New("OU (OR)", 2, new[] { "a", "b" }, true, false, false, false, logic);
                case NodeKind.Not: return New("NAO (NOT)", 1, new[] { "a" }, true, false, false, false, logic);

                case NodeKind.MoveX: return New("Mover X (vel)", 1, new[] { "vel" }, false, true, false, false, action);
                case NodeKind.MoveY: return New("Mover Y (vel)", 1, new[] { "vel" }, false, true, false, false, action);
                case NodeKind.MoveZ: return New("Mover Z (vel)", 1, new[] { "vel" }, false, true, false, false, action);
                case NodeKind.Grab: return New("Agarrar (se>0)", 1, new[] { "cond" }, false, true, false, false, action);
                case NodeKind.Release: return New("Soltar (se>0)", 1, new[] { "cond" }, false, true, false, false, action);

                case NodeKind.NeedBelow: return New("Precisa o de baixo?", 0, null, true, false, false, false, smart);
                case NodeKind.Search: return New("Procurar (auto)", 1, new[] { "ligar" }, false, true, false, false, smart);
                case NodeKind.Deliver: return New("Entregar no forno (auto)", 1, new[] { "ligar" }, false, true, false, false, smart);
            }
            return New("?", 0, null, true, false, false, false, Color.gray);
        }

        static NodeSpec New(string label, int inputs, string[] inputLabels, bool hasOutput,
            bool isSink, bool hasValue, bool hasVar, Color color)
        {
            return new NodeSpec
            {
                label = label,
                inputCount = inputs,
                inputLabels = inputLabels,
                hasOutput = hasOutput,
                isSink = isSink,
                hasValueField = hasValue,
                hasVarField = hasVar,
                color = color
            };
        }
    }

    /// <summary>Runtime instance of a node placed on the programming canvas.</summary>
    public class ProgramNode
    {
        public NodeKind kind;
        public NodeSpec spec;
        public RectTransform root;

        public ProgramNode[] inputSources;     // length = spec.inputCount
        public RectTransform[] inputPorts;
        public RectTransform outputPort;

        public InputField valueField;          // Constant
        public InputField varField;            // VarGet / VarSet

        // evaluation state
        public bool evaluating;
        public bool evaluated;
        public float cached;

        // persistent runtime state for self-contained "smart" blocks (e.g. the Search sweep
        // remembers which way it is currently travelling so it can bounce off the walls).
        public float dirX = 1f;
        public float dirZ = 1f;

        public float Value
        {
            get
            {
                if (valueField != null && float.TryParse(valueField.text,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v))
                    return v;
                return 0f;
            }
        }

        public string VarName => varField != null && !string.IsNullOrEmpty(varField.text) ? varField.text : "var";
    }
}
