using Graph.Core;

namespace Graph.Mongo;

public static class CheckpointEvaluator
{
    public static async Task<bool> CheckpointIsSatisfiedAsync(
        StateMapExecutor stateMap,
        Node checkpoint,
        ThreadContext threadContext,
        List<Guid> performedActionGuids,
        List<Guid> satisfiedCheckpointGuids
    )
    {
        var inputActions = (EdgeCollection<Node, Node>)(await checkpoint.GetValueAsync("input_actions"))!;
        var gateType = (string)(await checkpoint.GetValueAsync("gate_type"))!;

        if (gateType != "OR")
        {
            // All input actions must be performed before the checkpoint can be evaluated
            foreach (var inputActionGuid in inputActions.GetGuidsForRange(0, inputActions.Count))
                if (!performedActionGuids.Contains(inputActionGuid)) return false;
        }

        var dependencies = (EdgeCollection<Node, Node>)(await checkpoint.GetValueAsync("dependencies"))!;
        var checkpointReferences = (EdgeCollection<Node, Node>)(await checkpoint.GetValueAsync("checkpoint_references"))!;
        var numInputs = dependencies.Count + checkpointReferences.Count;
        var results = new List<bool>();

        foreach (var dependency in await dependencies.GetAllNodesAsync())
        {
            results.Add(await _DependencyIsSatisfiedAsync(
                stateMap,
                dependency,
                performedActionGuids,
                threadContext
            ));
            var evaluation = _EvaluateGate(gateType, numInputs, results);
            if (evaluation.IsComplete) return evaluation.Result;
        }

        foreach (var referencedCheckpointGuid in checkpointReferences.GetGuidsForRange(0, checkpointReferences.Count))
        {
            results.Add(satisfiedCheckpointGuids?.Contains(referencedCheckpointGuid) ?? false);
            var evaluation = _EvaluateGate(gateType, numInputs, results);
            if (evaluation.IsComplete) return evaluation.Result;
        }

        // Evaluation should always complete before this point
        throw new Exception($"Checkpoint evaluation failed (checkpoint guid: {checkpoint.Guid})");
    }

    private static async Task<bool> _DependencyIsSatisfiedAsync(
        StateMapExecutor stateMap,
        Node dependency,
        List<Guid> performedActionGuids,
        ThreadContext threadContext
    )
    {
        var sides = new List<string> { "left", "right" };
        var operandTypes = new Dictionary<string, Task<object>>();
        foreach (var side in sides)
            operandTypes.Add(side, dependency.GetValueAsync($"{side}_type")!);

        await Task.WhenAll(operandTypes.Values);

        var operandValues = new Dictionary<string, object?>();
        foreach (var side in sides)
        {
            if ((string)operandTypes[side].Result == "reference")
            {
                var reference = (string)(await dependency.GetValueAsync($"{side}_reference"))!;

                // If the action has not been performed, the dependency cannot be satisfied
                var actionGuid = await ReferenceResolver.ResolveReferenceAsync(
                    stateMap.Template,
                    reference,
                    fromEdgeCollection: "actions"
                );
                if (!performedActionGuids.Contains(actionGuid))
                    return false;

                var value = await ReferenceResolver.ResolveReferencePathAsync(stateMap, reference, threadContext);
                operandValues.Add(side, value);
            }
            else
            {
                var type = (string)operandTypes[side].Result;
                var value = await dependency.GetValueAsync($"{side}_{type}_value");
                operandValues.Add(side, value);
            }
        }

        var @operator = (string)(await dependency.GetValueAsync("operator"))!;
        return new Comparison(operandValues["left"], @operator, operandValues["right"]).Result;
    }

    private static GateEvaluation _EvaluateGate(string? gateType, int numInputs, List<bool> results)
    {
        if (gateType == null)
        {
            if (numInputs != 1 || results.Count != 1)
                throw new Exception("Invalid checkpoint: gate type is null but there are multiple inputs.");

            return new GateEvaluation
            {
                IsComplete = true,
                Result = results[0]
            };
        }

        var allResultsCollected = results.Count == numInputs;

        if (gateType == "AND")
        {
            if (results.Contains(false))
                return new GateEvaluation { IsComplete = true, Result = false };

            return new GateEvaluation
            {
                IsComplete = allResultsCollected,
                Result = true
            };
        }

        if (gateType == "OR")
        {
            if (results.Contains(true))
                return new GateEvaluation { IsComplete = true, Result = true };

            return new GateEvaluation
            {
                IsComplete = allResultsCollected,
                Result = false
            };
        }

        if (gateType == "XOR")
        {
            var numTrues = results.Count(r => r);
            return new GateEvaluation
            {
                IsComplete = allResultsCollected,
                Result = numTrues == 1
            };
        }

        if (gateType == "NAND")
        {
            if (results.Contains(false))
                return new GateEvaluation { IsComplete = true, Result = true };

            return new GateEvaluation
            {
                IsComplete = allResultsCollected,
                Result = false
            };
        }

        if (gateType == "NOR")
        {
            if (results.Contains(true))
                return new GateEvaluation { IsComplete = true, Result = false };

            return new GateEvaluation
            {
                IsComplete = allResultsCollected,
                Result = true
            };
        }

        throw new Exception($"Invalid gate type: {gateType}");
    }
}

public class GateEvaluation
{
    public bool IsComplete { get; set; }
    public bool Result { get; set; }
}