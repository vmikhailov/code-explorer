using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// High-performance, zero-dependency subword and n-gram feature hashing vector encoder for code identifiers.
/// Produces 128-dimensional L2-normalized float32 embeddings capable of sub-millisecond semantic and fuzzy code search.
/// </summary>
public static partial class IdentifierVectorEncoder
{
    public const int Dimension = 128;
    public const int VectorByteSize = Dimension * sizeof(float);

    [GeneratedRegex(@"([a-z0-9])([A-Z])|([A-Z]+)([A-Z][a-z])")]
    private static partial Regex CamelCaseSplitter();

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex PunctuationSplitter();

    /// <summary>
    /// Encodes a code identifier, route, or search query into a 128-dimensional L2-normalized vector.
    /// </summary>
    public static float[] Encode(string? text)
    {
        var vec = new float[Dimension];
        if (string.IsNullOrWhiteSpace(text))
        {
            return vec;
        }

        var trimmed = text.Trim();
        var words = Tokenize(trimmed);
        if (words.Count == 0)
        {
            return vec;
        }

        // 1. Full string match boost
        var fullNorm = string.Join("", words);
        HashFeature(vec, fullNorm, weight: 3.5f);

        // 2. Acronym boost (e.g., "gch" for "GateCommandHandler")
        if (words.Count > 1)
        {
            var acronym = new StringBuilder(words.Count);
            foreach (var w in words)
            {
                if (w.Length > 0) acronym.Append(w[0]);
            }
            HashFeature(vec, acronym.ToString(), weight: 2.5f);
        }

        // 3. Word tokens
        foreach (var word in words)
        {
            HashFeature(vec, word, weight: 2.0f);

            // 4. Character 3-grams and 4-grams within words for fuzzy typo tolerance
            if (word.Length >= 3)
            {
                for (int i = 0; i <= word.Length - 3; i++)
                {
                    HashFeature(vec, word.Substring(i, 3), weight: 1.0f);
                }
            }

            if (word.Length >= 4)
            {
                for (int i = 0; i <= word.Length - 4; i++)
                {
                    HashFeature(vec, word.Substring(i, 4), weight: 1.2f);
                }
            }
        }

        // 5. L2 Normalize
        Normalize(vec);
        return vec;
    }

    /// <summary>
    /// Computes cosine similarity between two 128-dimensional L2-normalized vectors.
    /// </summary>
    public static float CosineSimilarity(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
    {
        if (v1.Length != Dimension || v2.Length != Dimension)
        {
            return 0f;
        }

        float dot = 0f;
        int i = 0;

        if (Vector.IsHardwareAccelerated)
        {
            int vecSize = Vector<float>.Count;
            var sumVec = Vector<float>.Zero;

            while (i <= Dimension - vecSize)
            {
                var va = new Vector<float>(v1.Slice(i, vecSize));
                var vb = new Vector<float>(v2.Slice(i, vecSize));
                sumVec += va * vb;
                i += vecSize;
            }

            for (int k = 0; k < vecSize; k++)
            {
                dot += sumVec[k];
            }
        }

        for (; i < Dimension; i++)
        {
            dot += v1[i] * v2[i];
        }

        return Math.Clamp(dot, 0f, 1f);
    }

    public static byte[] ToBytes(float[] vec)
    {
        var bytes = new byte[VectorByteSize];
        Buffer.BlockCopy(vec, 0, bytes, 0, VectorByteSize);
        return bytes;
    }

    public static float[] FromBytes(ReadOnlySpan<byte> bytes)
    {
        var vec = new float[Dimension];
        if (bytes.Length >= VectorByteSize)
        {
            Buffer.BlockCopy(bytes.ToArray(), 0, vec, 0, VectorByteSize);
        }
        return vec;
    }

    public static List<string> Tokenize(string text)
    {
        // Split camelCase and PascalCase: "GateCommandHandler" -> "Gate Command Handler"
        var splitCase = CamelCaseSplitter().Replace(text, "$1$3 $2$4");
        // Split punctuation (slashes, dots, underscores, dashes): "/api/gates/check" -> "api gates check"
        var tokens = PunctuationSplitter().Split(splitCase);

        var result = new List<string>(tokens.Length);
        foreach (var t in tokens)
        {
            var cleaned = t.Trim().ToLowerInvariant();
            if (cleaned.Length > 0)
            {
                result.Add(cleaned);
            }
        }
        return result;
    }

    private static void HashFeature(float[] vec, string feature, float weight)
    {
        uint hash = Fnv1a32(feature);
        int dim = (int)(hash % Dimension);
        // Signed hash to ensure zero-mean distribution and reduce hash collision bias
        float sign = (hash & 0x80000000) == 0 ? 1.0f : -1.0f;
        vec[dim] += sign * weight;
    }

    private static uint Fnv1a32(string text)
    {
        uint hash = 2166136261;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= 16777619;
        }
        return hash;
    }

    private static void Normalize(float[] vec)
    {
        double sumSq = 0;
        for (int i = 0; i < Dimension; i++)
        {
            sumSq += (double)vec[i] * vec[i];
        }

        if (sumSq > 0)
        {
            float norm = (float)Math.Sqrt(sumSq);
            for (int i = 0; i < Dimension; i++)
            {
                vec[i] /= norm;
            }
        }
    }
}
