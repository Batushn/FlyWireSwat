using System;

namespace FlyWireSwat.Connectome
{
    /// <summary>
    /// Leaky integrate-and-fire parameters. Defaults follow Shiu et al. 2024 (Nature),
    /// "A Drosophila computational brain model reveals sensorimotor processing", which
    /// simulated the FlyWire connectome with exactly this kind of model.
    /// </summary>
    [Serializable]
    public struct LifParams
    {
        public float dtMs;            // integration step
        public float tauMembraneMs;   // membrane time constant
        public float tauSynMs;        // synaptic current decay
        public float vRest;           // mV
        public float vThreshold;      // mV
        public float vReset;          // mV
        public float refractoryMs;
        public float mvPerSynapse;    // PSP contribution per synapse
        public float noiseMv;         // membrane noise (std per sqrt(ms))

        public static LifParams Default => new LifParams
        {
            dtMs = 0.1f,
            tauMembraneMs = 20f,
            tauSynMs = 5f,
            vRest = -52f,
            vThreshold = -45f,
            vReset = -52f,
            refractoryMs = 2.2f,
            mvPerSynapse = 0.275f,
            noiseMv = 0.6f,
        };
    }

    /// <summary>How strongly each looming feature drives each visual projection neuron class.</summary>
    [Serializable]
    public struct SensoryTuning
    {
        // Ache et al. 2019 (Neuron): LC4 encodes angular velocity, LPLC2 encodes angular size.
        public float lc4GainMv;          // peak external drive (mV) for LC4
        public float lc4HalfDegPerSec;   // angular velocity at half-saturation
        public float lplc2GainMv;
        public float lplc2HalfDeg;       // angular size at half-saturation
        public float lplc1GainMv;        // LPLC1: loom (mixed size/velocity)
        public float lc6GainMv;          // LC6: loom, large-field
        public float lc16GainMv;         // LC16: loom / backward walking
        public float lc22GainMv;         // LC22: small objects, weak loom
        public float lc15GainMv;         // LC15: small moving objects
        public float rearVisibility;     // gain multiplier when the threat comes from behind
        public float minAngleDeg;        // stimulus below this angular size is invisible
        public float expansionHalfDegPerSec; // size-coding neurons only respond while the image expands
        public float dnInputGain;        // descending neurons are huge (low input resistance): PSPs are scaled down
        public float distractedProbability; // chance the fly is feeding/grooming and half-attends
        public float distractedVisibility;

        public static SensoryTuning Default => new SensoryTuning
        {
            lc4GainMv = 70f, lc4HalfDegPerSec = 400f,
            lplc2GainMv = 70f, lplc2HalfDeg = 35f,
            lplc1GainMv = 45f, lc6GainMv = 40f, lc16GainMv = 25f, lc22GainMv = 15f, lc15GainMv = 10f,
            rearVisibility = 0.45f, minAngleDeg = 0.5f,
            expansionHalfDegPerSec = 150f, dnInputGain = 0.5f,
            distractedProbability = 0.35f, distractedVisibility = 0.5f,
        };
    }
}
