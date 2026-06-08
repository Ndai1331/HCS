using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HC.Blazor.Components.Select2;

public partial class CommonSelect2 : IAsyncDisposable
{
    private const string JsModule = "hcCommonSelect2";

    private readonly string _selectId = "hc-cs2-" + Guid.NewGuid().ToString("n");

    private DotNetObjectReference<CommonSelect2>? _dotNetRef;
    private bool _initialized;
    private string _lastSyncedSignature = string.Empty;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Parameter]
    public IReadOnlyList<LookupDto<Guid>> Datasource { get; set; } = null!;

    [Parameter]
    public List<LookupDto<Guid>> Value { get; set; } = null!;

    [Parameter]
    public EventCallback<List<LookupDto<Guid>>> ValueChanged { get; set; }

    [Parameter]
    public Func<IReadOnlyList<LookupDto<Guid>>, string, CancellationToken, Task<List<LookupDto<Guid>>>> FilterFunction { get; set; } = null!;

    [Parameter]
    public Func<IReadOnlyList<LookupDto<Guid>>, string, CancellationToken, Task<LookupDto<Guid>?>> GetElementById { get; set; } = null!;

    [Parameter]
    public bool Multiselect { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    private readonly Dictionary<Guid, LookupDto<Guid>> _lookupCache = new();

    [JSInvokable]
    public async Task<HcCommonSelect2SearchResponse> SearchAsync(string term, int page)
    {
        var list = await FilterFunction(Datasource, term ?? string.Empty, CancellationToken.None);
        foreach (var x in list)
        {
            _lookupCache[x.Id] = x;
        }

        return new HcCommonSelect2SearchResponse
        {
            Results = list.Select(x => new HcCommonSelect2ResultDto
            {
                Id = x.Id.ToString(),
                Text = x.DisplayName ?? string.Empty
            }).ToList(),
            More = false
        };
    }

    [JSInvokable]
    public async Task OnSelectionChangeAsync(string[]? rawIds)
    {
        var ids = ParseIds(rawIds);
        var sig = Signature(ids);
        if (sig == _lastSyncedSignature)
        {
            return;
        }

        var list = await ResolveLookupsAsync(ids);
        await ValueChanged.InvokeAsync(list);
        _lastSyncedSignature = sig;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            var safeValue = Value ?? new List<LookupDto<Guid>>();
            var initial = BuildItemPayloads(safeValue);
            await JS.InvokeVoidAsync($"{JsModule}.init", _selectId, _dotNetRef, new
            {
                multiselect = Multiselect,
                placeholder = Placeholder ?? string.Empty,
                minimumInputLength = 0
            }, initial);
            _initialized = true;
            _lastSyncedSignature = Signature(safeValue.Select(v => v.Id));
        }
        else if (_initialized)
        {
            var safeValue = Value ?? new List<LookupDto<Guid>>();
            var sig = Signature(safeValue.Select(v => v.Id));
            if (sig != _lastSyncedSignature)
            {
                _lastSyncedSignature = sig;
                var payload = BuildItemPayloads(safeValue);
                await JS.InvokeVoidAsync($"{JsModule}.setSelection", _selectId, payload, Multiselect);
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private static string Signature(IEnumerable<Guid> ids) =>
        string.Join(",", ids.OrderBy(x => x));

    private static List<Guid> ParseIds(string[]? raw)
    {
        var list = new List<Guid>();
        if (raw == null)
        {
            return list;
        }

        foreach (var s in raw)
        {
            if (Guid.TryParse(s, out var g))
            {
                list.Add(g);
            }
        }

        return list;
    }

    private async Task<List<LookupDto<Guid>>> ResolveLookupsAsync(List<Guid> ids)
    {
        var result = new List<LookupDto<Guid>>();
        foreach (var id in ids)
        {
            if (_lookupCache.TryGetValue(id, out var dto))
            {
                result.Add(dto);
                continue;
            }

            var resolved = await GetElementById(Datasource, id.ToString(), CancellationToken.None);
            if (resolved != null)
            {
                _lookupCache[id] = resolved;
                result.Add(resolved);
            }
        }

        return result;
    }

    private List<HcCommonSelect2ResultDto> BuildItemPayloads(List<LookupDto<Guid>> values)
    {
        var list = new List<HcCommonSelect2ResultDto>();
        foreach (var v in values)
        {
            _lookupCache[v.Id] = v;
            list.Add(new HcCommonSelect2ResultDto
            {
                Id = v.Id.ToString(),
                Text = v.DisplayName ?? string.Empty
            });
        }

        return list;
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try
            {
                await JS.InvokeVoidAsync($"{JsModule}.destroy", _selectId);
            }
            catch (JSDisconnectedException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }

        _dotNetRef?.Dispose();
    }
}

public sealed class HcCommonSelect2SearchResponse
{
    public List<HcCommonSelect2ResultDto> Results { get; set; } = new();

    public bool More { get; set; }
}

public sealed class HcCommonSelect2ResultDto
{
    public string Id { get; set; } = "";

    public string Text { get; set; } = "";
}
