using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using ClrDiagnostics;
using DiagnosticModels;

namespace DiagnosticInvestigations;

public class KnownQuery
{
    public KnownQuery()
    {
    }

    public KnownQuery(Type type, string name, Func<DiagnosticAnalyzer, IEnumerable> populate,
        Func<object, string, bool?> filter)
    {
        this.Type = type;
        this.Name = name;
        this.Populate = populate;
        this.Filter = filter;
    }

    public Type? Type { get; set; }
    public string? Name { get; set; }
    public Func<DiagnosticAnalyzer, IEnumerable>? Populate { get; set; }
    public Func<object, string, bool?>? Filter { get; set; }

    /// <summary>
    /// True if this query supports a detail grid (master-detail pattern).
    /// Details are available via a "Details" property on each master row,
    /// or via the type's inherent children (e.g., ClrObject graphs).
    /// </summary>
    public bool HasDetails { get; set; }

    /// <summary>
    /// The CLR type of the detail rows, if HasDetails is true.
    /// Used by the client to select the correct detail column definitions.
    /// </summary>
    public Type? DetailType { get; set; }

    /// <summary>
    /// Property name on each master row that yields the detail rows (e.g., "Objects", "StackFrames").
    /// Null if HasDetails is false.
    /// </summary>
    public string? DetailProperty { get; set; }

    /// <summary>
    /// Executes the query against the given analyzer, optionally filtering results,
    /// and wraps the output in a serializable QueryResult.
    /// Exceptions during populate/filter are caught and returned with empty rows
    /// so the client always receives a valid response.
    /// </summary>
    public QueryResult ToQueryResult(DiagnosticAnalyzer analyzer, string? filter)
    {
        if (Populate is null)
            throw new InvalidOperationException($"Query '{Name}' has no Populate function.");

        IEnumerable rows;
        try
        {
            rows = Populate(analyzer);
        }
        catch (Exception ex)
        {
            // Populate can fail if the ClrMD heap is in an inconsistent state
            // or if the underlying data target has been disposed.
            return new QueryResult
            {
                QueryName = Name ?? string.Empty,
                ResultType = Type?.FullName ?? string.Empty,
                Rows = Array.Empty<object>(),
                HasDetails = HasDetails,
                DetailType = DetailType?.FullName,
                DetailProperty = DetailProperty,
            };
        }

        if (!string.IsNullOrWhiteSpace(filter) && Filter is not null)
        {
            try
            {
                rows = rows.Cast<object>().Where(o => Filter(o, filter) == true).ToList();
            }
            catch
            {
                // Filter evaluation can fail for objects with inaccessible properties.
                // Return unfiltered rows as a fallback.
            }
        }

        return new QueryResult
        {
            QueryName = Name ?? string.Empty,
            ResultType = Type?.FullName ?? string.Empty,
            Rows = rows,
            HasDetails = HasDetails,
            DetailType = DetailType?.FullName,
            DetailProperty = DetailProperty,
        };
    }

    /// <summary>
    /// Returns metadata about this query, including column definitions for rendering DataGrids.
    /// Column definitions mirror the WPF KnownGrids registry.
    /// </summary>
    public QueryMetadata GetMetadata()
    {
        var metadata = new QueryMetadata
        {
            QueryName = Name ?? string.Empty,
            ResultType = Type?.FullName ?? string.Empty,
            HasDetails = HasDetails,
            DetailType = DetailType?.FullName,
            DetailProperty = DetailProperty,
        };

        // Populate column definitions from the query type
        QueryMetadataFactory.PopulateMetadata(metadata);

        return metadata;
    }
}
