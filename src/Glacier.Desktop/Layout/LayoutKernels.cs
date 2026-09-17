namespace Glacier.Desktop.Layout;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

/// <summary>
/// High-performance SIMD layout arranger executing O(log2 N) Kogge-Stone prefix-sum scans.
/// Measures and arranges thousands of sibling layout boxes in sub-millisecond time.
/// </summary>
public static unsafe class LayoutKernels
{
    // AVX2 Shuffle Indices and Bitmasks for Kogge-Stone Scan (8 single-precision floats)
    private static readonly Vector256<int> Shift1Indices = Vector256.Create(0, 0, 1, 2, 3, 4, 5, 6);
    private static readonly Vector256<int> Shift2Indices = Vector256.Create(0, 0, 0, 1, 2, 3, 4, 5);
    private static readonly Vector256<int> Shift4Indices = Vector256.Create(0, 0, 0, 0, 0, 1, 2, 3);

    // Bitmasks with all-ones in active lanes and 0 in shifted-in zero lanes
    private static readonly Vector256<float> Mask1 = Vector256.Create(0, -1, -1, -1, -1, -1, -1, -1).AsSingle();
    private static readonly Vector256<float> Mask2 = Vector256.Create(0, 0, -1, -1, -1, -1, -1, -1).AsSingle();
    private static readonly Vector256<float> Mask4 = Vector256.Create(0, 0, 0, 0, -1, -1, -1, -1).AsSingle();

    // AVX-512 Shuffle Indices and Bitmasks for 16-element Kogge-Stone Scan
    private static readonly Vector512<int> Shift1Indices512 = Vector512.Create(0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
    private static readonly Vector512<int> Shift2Indices512 = Vector512.Create(0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13);
    private static readonly Vector512<int> Shift4Indices512 = Vector512.Create(0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
    private static readonly Vector512<int> Shift8Indices512 = Vector512.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7);

    private static readonly Vector512<float> Mask1_512 = Vector512.Create(0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1).AsSingle();
    private static readonly Vector512<float> Mask2_512 = Vector512.Create(0, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1).AsSingle();
    private static readonly Vector512<float> Mask4_512 = Vector512.Create(0, 0, 0, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1).AsSingle();
    private static readonly Vector512<float> Mask8_512 = Vector512.Create(0, 0, 0, 0, 0, 0, 0, 0, -1, -1, -1, -1, -1, -1, -1, -1).AsSingle();

    /// <summary>
    /// Computes horizontal layout arrangements using multi-tier AVX-512 and AVX2 parallel prefix scans.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ArrangeHorizontalRow(
        Span<LayoutBox> boxes, 
        float startX, 
        float spacing)
    {
        int count = boxes.Length;
        if (count == 0) return;

        fixed (LayoutBox* pBoxes = boxes)
        {
            float currentX = startX;
            int i = 0;

            // Tier 1: AVX-512 16-element Kogge-Stone Parallel Scan
            if (Avx512F.IsSupported && count >= 16)
            {
                var vSpacing512 = Vector512.Create(spacing);
                var vZero512 = Vector512<float>.Zero;
                float* pActualX512 = stackalloc float[16];

                for (; i <= count - 16; i += 16)
                {
                    var vRaw512 = Vector512.Create(
                        pBoxes[i + 0].DesiredWidth,  pBoxes[i + 1].DesiredWidth,
                        pBoxes[i + 2].DesiredWidth,  pBoxes[i + 3].DesiredWidth,
                        pBoxes[i + 4].DesiredWidth,  pBoxes[i + 5].DesiredWidth,
                        pBoxes[i + 6].DesiredWidth,  pBoxes[i + 7].DesiredWidth,
                        pBoxes[i + 8].DesiredWidth,  pBoxes[i + 9].DesiredWidth,
                        pBoxes[i + 10].DesiredWidth, pBoxes[i + 11].DesiredWidth,
                        pBoxes[i + 12].DesiredWidth, pBoxes[i + 13].DesiredWidth,
                        pBoxes[i + 14].DesiredWidth, pBoxes[i + 15].DesiredWidth
                    );

                    var vClamped512 = Vector512.Max(vRaw512, vZero512);
                    var v512 = Vector512.Add(vClamped512, vSpacing512);

                    // Step 1: Shift right by 1
                    var s1_512 = Vector512.Shuffle(v512, Shift1Indices512);
                    s1_512 = Vector512.ConditionalSelect(Mask1_512, s1_512, vZero512);
                    var v1_512 = Vector512.Add(v512, s1_512);

                    // Step 2: Shift right by 2
                    var s2_512 = Vector512.Shuffle(v1_512, Shift2Indices512);
                    s2_512 = Vector512.ConditionalSelect(Mask2_512, s2_512, vZero512);
                    var v2_512 = Vector512.Add(v1_512, s2_512);

                    // Step 3: Shift right by 4
                    var s4_512 = Vector512.Shuffle(v2_512, Shift4Indices512);
                    s4_512 = Vector512.ConditionalSelect(Mask4_512, s4_512, vZero512);
                    var v4_512 = Vector512.Add(v2_512, s4_512);

                    // Step 4: Shift right by 8 -> inclusive prefix sum
                    var s8_512 = Vector512.Shuffle(v4_512, Shift8Indices512);
                    s8_512 = Vector512.ConditionalSelect(Mask8_512, s8_512, vZero512);
                    var vInclusive512 = Vector512.Add(v4_512, s8_512);

                    // Exclusive prefix sum: shift inclusive right by 1
                    var sEx512 = Vector512.Shuffle(vInclusive512, Shift1Indices512);
                    var vEx512 = Vector512.ConditionalSelect(Mask1_512, sEx512, vZero512);
                    var vActualX512 = Vector512.Add(vEx512, Vector512.Create(currentX));

                    Vector512.Store(vActualX512, pActualX512);

                    for (int lane = 0; lane < 16; lane++)
                    {
                        pBoxes[i + lane].ActualX = pActualX512[lane];
                    }

                    currentX += vInclusive512.GetElement(15);
                }
            }

            // Tier 2: AVX2 Vector256<float> 8-element Kogge-Stone Parallel Scan
            if (Avx2.IsSupported && count - i >= 8)
            {
                var vSpacing = Vector256.Create(spacing);
                var vZero = Vector256<float>.Zero;
                float* pActualX256 = stackalloc float[8];

                for (; i <= count - 8; i += 8)
                {
                    var vRaw = Vector256.Create(
                        pBoxes[i + 0].DesiredWidth, pBoxes[i + 1].DesiredWidth,
                        pBoxes[i + 2].DesiredWidth, pBoxes[i + 3].DesiredWidth,
                        pBoxes[i + 4].DesiredWidth, pBoxes[i + 5].DesiredWidth,
                        pBoxes[i + 6].DesiredWidth, pBoxes[i + 7].DesiredWidth
                    );

                    var vClamped = Vector256.Max(vRaw, vZero);
                    var v = Vector256.Add(vClamped, vSpacing);

                    // Step 1: Shift right by 1
                    var s1 = Vector256.Shuffle(v, Shift1Indices);
                    s1 = Vector256.ConditionalSelect(Mask1, s1, vZero);
                    var v1 = Vector256.Add(v, s1);

                    // Step 2: Shift right by 2
                    var s2 = Vector256.Shuffle(v1, Shift2Indices);
                    s2 = Vector256.ConditionalSelect(Mask2, s2, vZero);
                    var v2 = Vector256.Add(v1, s2);

                    // Step 3: Shift right by 4 -> inclusive prefix sum
                    var s4 = Vector256.Shuffle(v2, Shift4Indices);
                    s4 = Vector256.ConditionalSelect(Mask4, s4, vZero);
                    var vInclusive = Vector256.Add(v2, s4);

                    // Exclusive prefix sum: shift inclusive right by 1
                    var sExclusive = Vector256.Shuffle(vInclusive, Shift1Indices);
                    var vExclusive = Vector256.ConditionalSelect(Mask1, sExclusive, vZero);
                    var vActualX = Vector256.Add(vExclusive, Vector256.Create(currentX));

                    Vector256.Store(vActualX, pActualX256);

                    for (int lane = 0; lane < 8; lane++)
                    {
                        pBoxes[i + lane].ActualX = pActualX256[lane];
                    }

                    currentX += vInclusive.GetElement(7);
                }
            }

            // Tier 3: Remainder scalar tail loop (100% element preserving)
            for (; i < count; i++)
            {
                pBoxes[i].ActualX = currentX;
                float w = MathF.Max(0f, pBoxes[i].DesiredWidth) + spacing;
                currentX += w;
            }
        }
    }

    /// <summary>
    /// Computes horizontal layout arrangements directly on Structure-of-Arrays (SoA) layout buffers.
    /// Eliminates all gather/scatter overhead and executes contiguous vector loads and stores.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ArrangeHorizontalRowSoA(
        ReadOnlySpan<float> desiredWidths,
        Span<float> actualXs,
        float startX,
        float spacing)
    {
        int count = desiredWidths.Length;
        if (count == 0 || actualXs.Length < count) return;

        fixed (float* pWidths = desiredWidths)
        fixed (float* pActualX = actualXs)
        {
            float currentX = startX;
            int i = 0;

            // Tier 1: AVX-512 16-element Kogge-Stone Parallel Scan
            if (Avx512F.IsSupported && count >= 16)
            {
                var vSpacing512 = Vector512.Create(spacing);
                var vZero512 = Vector512<float>.Zero;

                for (; i <= count - 16; i += 16)
                {
                    var vRaw512 = Vector512.Load(pWidths + i);
                    var vClamped512 = Vector512.Max(vRaw512, vZero512);
                    var v512 = Vector512.Add(vClamped512, vSpacing512);

                    // Step 1: Shift right by 1
                    var s1_512 = Vector512.Shuffle(v512, Shift1Indices512);
                    s1_512 = Vector512.ConditionalSelect(Mask1_512, s1_512, vZero512);
                    var v1_512 = Vector512.Add(v512, s1_512);

                    // Step 2: Shift right by 2
                    var s2_512 = Vector512.Shuffle(v1_512, Shift2Indices512);
                    s2_512 = Vector512.ConditionalSelect(Mask2_512, s2_512, vZero512);
                    var v2_512 = Vector512.Add(v1_512, s2_512);

                    // Step 3: Shift right by 4
                    var s4_512 = Vector512.Shuffle(v2_512, Shift4Indices512);
                    s4_512 = Vector512.ConditionalSelect(Mask4_512, s4_512, vZero512);
                    var v4_512 = Vector512.Add(v2_512, s4_512);

                    // Step 4: Shift right by 8 -> inclusive prefix sum
                    var s8_512 = Vector512.Shuffle(v4_512, Shift8Indices512);
                    s8_512 = Vector512.ConditionalSelect(Mask8_512, s8_512, vZero512);
                    var vInclusive512 = Vector512.Add(v4_512, s8_512);

                    // Exclusive prefix sum: shift inclusive right by 1
                    var sEx512 = Vector512.Shuffle(vInclusive512, Shift1Indices512);
                    var vEx512 = Vector512.ConditionalSelect(Mask1_512, sEx512, vZero512);
                    var vActualX512 = Vector512.Add(vEx512, Vector512.Create(currentX));

                    Vector512.Store(vActualX512, pActualX + i);

                    currentX += vInclusive512.GetElement(15);
                }
            }

            // Tier 2: AVX2 Vector256<float> 8-element Kogge-Stone Parallel Scan
            if (Avx2.IsSupported && count - i >= 8)
            {
                var vSpacing = Vector256.Create(spacing);
                var vZero = Vector256<float>.Zero;

                for (; i <= count - 8; i += 8)
                {
                    var vRaw = Vector256.Load(pWidths + i);
                    var vClamped = Vector256.Max(vRaw, vZero);
                    var v = Vector256.Add(vClamped, vSpacing);

                    var s1 = Vector256.Shuffle(v, Shift1Indices);
                    s1 = Vector256.ConditionalSelect(Mask1, s1, vZero);
                    var v1 = Vector256.Add(v, s1);

                    var s2 = Vector256.Shuffle(v1, Shift2Indices);
                    s2 = Vector256.ConditionalSelect(Mask2, s2, vZero);
                    var v2 = Vector256.Add(v1, s2);

                    var s4 = Vector256.Shuffle(v2, Shift4Indices);
                    s4 = Vector256.ConditionalSelect(Mask4, s4, vZero);
                    var vInclusive = Vector256.Add(v2, s4);

                    var sExclusive = Vector256.Shuffle(vInclusive, Shift1Indices);
                    var vExclusive = Vector256.ConditionalSelect(Mask1, sExclusive, vZero);
                    var vActualX = Vector256.Add(vExclusive, Vector256.Create(currentX));

                    Vector256.Store(vActualX, pActualX + i);

                    currentX += vInclusive.GetElement(7);
                }
            }

            // Tier 3: Remainder scalar tail loop
            for (; i < count; i++)
            {
                pActualX[i] = currentX;
                float w = MathF.Max(0f, pWidths[i]) + spacing;
                currentX += w;
            }
        }
    }
}
