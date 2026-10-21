namespace OfficeBuddy.Core;

/// <summary>
/// Saves several files into one folder all or nothing: if any of them can't be written, no new
/// file is left behind and existing files of the same names are unchanged.
/// </summary>
internal static class SeveralFiles
{
    /// <param name="writeFile">Writes file number <c>i</c> (the index into <paramref name="fileNames"/>) to the given path.</param>
    /// <returns>The paths of the files written, in the order of <paramref name="fileNames"/>.</returns>
    public static async Task<IReadOnlyList<string>> WriteAsync(
        string folder, IReadOnlyList<string> fileNames, Func<int, string, Task> writeFile)
    {
        var targets = fileNames.Select(name => Path.Combine(folder, name)).ToList();
        var temporary = fileNames.Select(name => Path.Combine(folder, $"~{name}.{Guid.NewGuid():N}.tmp")).ToList();
        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                await writeFile(i, temporary[i]);
            }
            PutInPlace(temporary, targets);
            return targets;
        }
        finally
        {
            foreach (var path in temporary)
            {
                TryDelete(path);
            }
        }
    }

    /// <summary>Moves every written file to its name, putting back what was there if one of them fails.</summary>
    private static void PutInPlace(IReadOnlyList<string> temporary, IReadOnlyList<string> targets)
    {
        var done = new List<(string Target, string? Backup, bool Placed)>();
        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                string? backup = null;
                if (File.Exists(targets[i]))
                {
                    backup = $"{temporary[i]}.alt";
                    File.Move(targets[i], backup);
                }
                done.Add((targets[i], backup, false));
                File.Move(temporary[i], targets[i]);
                done[^1] = (targets[i], backup, true);
            }
        }
        catch (Exception)
        {
            foreach (var (target, backup, placed) in Enumerable.Reverse(done))
            {
                if (placed)
                {
                    TryDelete(target);
                }
                if (backup is not null)
                {
                    File.Move(backup, target);
                }
            }
            throw;
        }

        foreach (var (_, backup, _) in done)
        {
            if (backup is not null)
            {
                TryDelete(backup);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary file is harmless; the save itself already succeeded or failed.
        }
    }
}
