using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Features.Inspect;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.TUI.Pages.InspectAssets;

public enum InspectAssetsOperation
{
    ListAssets,
    ShowFields
}

public abstract record InspectAssetsState
{
    public sealed record ActionMenu : InspectAssetsState;

    public sealed record EnterListPath : InspectAssetsState;

    public sealed record SelectLimit(string AssetsFilePath) : InspectAssetsState;

    public sealed record EnterCustomLimit(string AssetsFilePath) : InspectAssetsState;

    public sealed record EnterFields : InspectAssetsState;

    public sealed record Working(InspectAssetsOperation Operation) : InspectAssetsState;

    public sealed record Assets(InspectListResult Result) : InspectAssetsState;

    public sealed record Fields(AssetField FieldTree) : InspectAssetsState;

    public sealed record Failed(InspectAssetsOperation Operation, OperationError Error) : InspectAssetsState;
}

public sealed class InspectAssetsLogic : TerminalPageLogic<InspectAssetsState>
{
    public InspectAssetsLogic(IServiceScopeFactory scopeFactory)
        : base(scopeFactory, new InspectAssetsState.ActionMenu()) { }

    public void ShowActionMenu()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return;
            }

            CurrentState = new InspectAssetsState.ActionMenu();
        }
    }

    public void ShowListPathInput()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return;
            }

            if (CurrentState is not (InspectAssetsState.ActionMenu or InspectAssetsState.SelectLimit))
            {
                throw new InvalidOperationException("The list path input is not available.");
            }

            CurrentState = new InspectAssetsState.EnterListPath();
        }
    }

    public void SubmitListPath(string assetsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);

        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return;
            }

            if (CurrentState is not InspectAssetsState.EnterListPath)
            {
                throw new InvalidOperationException("An assets file path is not expected.");
            }

            CurrentState = new InspectAssetsState.SelectLimit(assetsFilePath);
        }
    }

    public void ShowCustomLimitInput()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return;
            }

            if (CurrentState is not InspectAssetsState.SelectLimit state)
            {
                throw new InvalidOperationException("A custom limit is not available.");
            }

            CurrentState = new InspectAssetsState.EnterCustomLimit(state.AssetsFilePath);
        }
    }

    public void ReturnToLimitChoices()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return;
            }

            if (CurrentState is not InspectAssetsState.EnterCustomLimit state)
            {
                throw new InvalidOperationException("The limit choices are not available.");
            }

            CurrentState = new InspectAssetsState.SelectLimit(state.AssetsFilePath);
        }
    }

    public void ShowFieldsInput()
    {
        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return;
            }

            if (CurrentState is not InspectAssetsState.ActionMenu)
            {
                throw new InvalidOperationException("The fields input is not available.");
            }

            CurrentState = new InspectAssetsState.EnterFields();
        }
    }

    public Task InspectListAsync(int? limit)
    {
        if (limit is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            string assetsFilePath = CurrentState switch
            {
                InspectAssetsState.SelectLimit state => state.AssetsFilePath,
                InspectAssetsState.EnterCustomLimit state => state.AssetsFilePath,
                _ => throw new InvalidOperationException("An assets list is not ready for inspection.")
            };

            return StartInspection<InspectListRequest, InspectListResult>(
                new InspectListRequest(assetsFilePath, limit),
                InspectAssetsOperation.ListAssets,
                CompleteList);
        }
    }

    public Task InspectFieldsAsync(string assetsFilePath, long pathId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);

        lock (SyncRoot)
        {
            if (IsUnavailable)
            {
                return Task.CompletedTask;
            }

            if (CurrentState is not InspectAssetsState.EnterFields)
            {
                throw new InvalidOperationException("Asset fields are not ready for inspection.");
            }

            return StartInspection<InspectFieldsRequest, AssetField>(
                new InspectFieldsRequest(assetsFilePath, pathId),
                InspectAssetsOperation.ShowFields,
                CompleteFields);
        }
    }

    private Task StartInspection<TRequest, TResult>(
        TRequest request,
        InspectAssetsOperation operation,
        Action<OperationResult<TResult>> complete)
        where TRequest : IRequest<OperationResult<TResult>>
    {
        CurrentState = new InspectAssetsState.Working(operation);
        return StartOperation<TRequest, TResult>(request, result =>
        {
            if (result is OperationFailed<TResult> failed)
            {
                CurrentState = new InspectAssetsState.Failed(operation, failed.Error);
                return;
            }

            complete(result);
        });
    }

    private void CompleteList(OperationResult<InspectListResult> result)
    {
        var succeeded = (OperationSucceeded<InspectListResult>)result;
        CurrentState = new InspectAssetsState.Assets(succeeded.Value);
    }

    private void CompleteFields(OperationResult<AssetField> result)
    {
        var succeeded = (OperationSucceeded<AssetField>)result;
        CurrentState = new InspectAssetsState.Fields(succeeded.Value);
    }
}
