using System.Collections.Generic;
using Newtonsoft.Json;

namespace FlyWireSwat.Connectome
{
    /// <summary>Raw JSON schema produced by Tools/extract_escape_circuit.py (FlyWire FAFB v783).</summary>
    public class CircuitSummary
    {
        public string source;
        public int neuronCount;
        public int edgeCount;
        public int totalSynapses;
        public Dictionary<string, int> typeCounts;
    }

    public class NeuronInfo
    {
        public string id;          // FlyWire root id (720575940...)
        public string type;        // consolidated cell type, e.g. LC4, LPLC2, DNp01
        public string nt;          // predicted neurotransmitter
        public float ntScore;
        public string side;        // left / right / center
        public string superClass;
        public string cls;
        public bool isSeed;
        public string role;        // DN / VPN / other
    }

    public class EdgeInfo
    {
        public int pre;
        public int post;
        public int syn;
        public string nt;
        public float sign;         // +1 ACH, -1 GABA/GLUT, +0.3 modulatory
    }

    public class CircuitData
    {
        public CircuitSummary summary;
        public List<NeuronInfo> neurons;
        public List<EdgeInfo> edges;

        public static CircuitData FromJson(string json) => JsonConvert.DeserializeObject<CircuitData>(json);
    }

    /// <summary>Compact per-neuron category codes used inside Burst jobs.</summary>
    public static class NeuronKind
    {
        public const byte Other = 0;
        public const byte LC4 = 1;
        public const byte LPLC2 = 2;
        public const byte LPLC1 = 3;
        public const byte LC6 = 4;
        public const byte LC16 = 5;
        public const byte LC22 = 6;
        public const byte LC15 = 7;
        public const byte GF = 10;      // DNp01, Giant Fiber
        public const byte DNp02 = 11;
        public const byte DNp04 = 12;
        public const byte DNp06 = 13;
        public const byte DNp11 = 14;

        public static byte FromType(string t)
        {
            switch (t)
            {
                case "LC4": return LC4;
                case "LPLC2": return LPLC2;
                case "LPLC1": return LPLC1;
                case "LC6": return LC6;
                case "LC16": return LC16;
                case "LC22": return LC22;
                case "LC15": return LC15;
                case "DNp01": return GF;
                case "DNp02": return DNp02;
                case "DNp04": return DNp04;
                case "DNp06": return DNp06;
                case "DNp11": return DNp11;
                default: return Other;
            }
        }

        public static bool IsVisualProjection(byte k) => k >= LC4 && k <= LC15;
        public static bool IsDescending(byte k) => k >= GF;
    }
}
