using System.Media;

namespace LNXcounter.Services;

/// <summary>A short bright "treasure" chime made in code (a rising four-note arpeggio), so no sound file is needed.</summary>
public static class Chime
{
    static byte[]? wav;

    public static void Play()
    {
        try
        {
            wav ??= Build();
            var p = new SoundPlayer(new MemoryStream(wav));
            p.Play();   // asynchronous
        }
        catch { }
    }

    static byte[] Build()
    {
        const int rate = 44100;
        double[] notes = { 659.25, 880.0, 1108.73, 1318.51 };   // E5 A5 C#6 E6
        int total = (int)(rate * 1.5);
        var pcm = new double[total];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(rate * 0.13 * n);
            for (int i = 0; start + i < total; i++)
            {
                double t = i / (double)rate;
                double env = Math.Min(1, t / 0.004) * Math.Exp(-t * (n == notes.Length - 1 ? 2.6 : 5.5));
                double s = Math.Sin(2 * Math.PI * notes[n] * t) + 0.35 * Math.Sin(4 * Math.PI * notes[n] * t) + 0.12 * Math.Sin(6 * Math.PI * notes[n] * t);
                pcm[start + i] += s * env * 0.22;
            }
        }
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray()); w.Write(36 + total * 2); w.Write("WAVEfmt "u8.ToArray());
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(total * 2);
        foreach (var v in pcm) w.Write((short)(Math.Clamp(v, -1, 1) * 32000));
        w.Flush();
        return ms.ToArray();
    }
}
