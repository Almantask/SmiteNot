using GodGame.Sim;

namespace GodGame.Sim.Hosting;

/// <summary>
/// Keyframes the timeline's memory budget pushes out, kept as one file per tick in a directory (tech notes, section 6:
/// spill older keyframes to disk on the floor machine). The directory is the caller's: a scratch folder per preview.
/// </summary>
public sealed class DirectoryKeyframeSpill : IKeyframeSpill
{
    private readonly string _directory;

    public DirectoryKeyframeSpill(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Write(int tick, byte[] keyframe) => File.WriteAllBytes(PathOf(tick), keyframe);

    public byte[] Read(int tick) => File.ReadAllBytes(PathOf(tick));

    public void Delete(int tick) => File.Delete(PathOf(tick));

    private string PathOf(int tick) =>
        Path.Combine(
            _directory,
            tick.ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + ".keyframe"
        );
}
