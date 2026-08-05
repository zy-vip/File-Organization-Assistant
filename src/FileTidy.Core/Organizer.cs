using FileTidy.Core.Models;

namespace FileTidy.Core;

/// <summary>单个文件的执行结果项</summary>
public class OrganizeItem
{
    public required string Source { get; init; }
    public string? Dest { get; init; }
    public required string Reason { get; init; }
}

/// <summary>一次整理的执行结果</summary>
public class OrganizeResult
{
    public int Succeeded { get; set; }
    public List<OrganizeItem> Skipped { get; set; } = new();
    public List<OrganizeItem> Failed { get; set; } = new();
}

/// <summary>执行整理：移动文件、跨卷处理、失败不中断、产出操作记录</summary>
public static class Organizer
{
    /// <summary>按预览执行移动。对每条 Moved 条目：再校验（仍存在、仍匹配）；
    /// 移动成功计入 Succeeded；文件缺失/不再匹配计入 Skipped；IO 失败计入 Failed。
    /// 返回结果与本次操作记录（供调用方持久化）。</summary>
    public static (OrganizeResult Result, OperationRecord Record) Execute(IReadOnlyList<PreviewEntry> previews, DateTime now)
    {
        var result = new OrganizeResult();
        var record = new OperationRecord { Timestamp = now };

        foreach (var p in previews)
        {
            if (p.Status != PreviewStatus.Moved || p.DestPath is null) continue;

            if (!File.Exists(p.File.FullPath))
            {
                result.Skipped.Add(new OrganizeItem { Source = p.File.FullPath, Dest = p.DestPath, Reason = "文件已不存在" });
                continue;
            }
            if (p.MatchedRule is null || !RuleEngine.IsMatch(p.MatchedRule, p.File, now))
            {
                result.Skipped.Add(new OrganizeItem { Source = p.File.FullPath, Dest = p.DestPath, Reason = "不再匹配规则" });
                continue;
            }

            try
            {
                var finalDest = p.DestPath;
                if (p.MatchedRule.AutoRenameOnConflict)
                    finalDest = PathHelper.GetUniquePath(p.DestPath);

                Directory.CreateDirectory(Path.GetDirectoryName(finalDest)!);
                Move(p.File.FullPath, finalDest);
                result.Succeeded++;
                record.Entries.Add(new LogEntry { Source = p.File.FullPath, Dest = finalDest });
            }
            catch (IOException ex)
            {
                result.Failed.Add(new OrganizeItem { Source = p.File.FullPath, Dest = p.DestPath, Reason = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Failed.Add(new OrganizeItem { Source = p.File.FullPath, Dest = p.DestPath, Reason = ex.Message });
            }
        }
        return (result, record);
    }

    private static void Move(string source, string dest)
    {
        if (string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(dest)), StringComparison.OrdinalIgnoreCase))
        {
            File.Move(source, dest);
            return;
        }
        File.Copy(source, dest);
        try
        {
            File.Delete(source);
        }
        catch
        {
            File.Delete(dest); // 副本回滚，避免源目标双份
            throw;
        }
    }

    /// <summary>撤销结果</summary>
    public class UndoResult
    {
        public int Restored { get; set; }
        public List<OrganizeItem> Skipped { get; set; } = new();
    }

    /// <summary>按记录反向移动还原文件；目标文件缺失则跳过并记入 Skipped，不中断其余条目</summary>
    public static UndoResult Undo(OperationRecord record)
    {
        var result = new UndoResult();
        foreach (var entry in record.Entries.AsEnumerable().Reverse())
        {
            if (!File.Exists(entry.Dest))
            {
                result.Skipped.Add(new OrganizeItem { Source = entry.Source, Dest = entry.Dest, Reason = "文件已不存在" });
                continue;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.Source)!);
                Move(entry.Dest, entry.Source);
                result.Restored++;
            }
            catch (IOException ex)
            {
                result.Skipped.Add(new OrganizeItem { Source = entry.Source, Dest = entry.Dest, Reason = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Skipped.Add(new OrganizeItem { Source = entry.Source, Dest = entry.Dest, Reason = ex.Message });
            }
        }
        return result;
    }
}