namespace Qx.Headers.Flash;

public sealed class Avm2LoopAnalysis
{
    public required List<Avm2DominatorInventory> Dominators { get; init; }
    public required List<Avm2NaturalLoopInventory> NaturalLoops { get; init; }
    public required List<Avm2IrreducibleCycleInventory> IrreducibleCycles { get; init; }
    public required bool HasLoop { get; init; }
}

public static class Avm2LoopAnalyzer
{
    sealed class NaturalLoop
    {
        public required int Header { get; init; }
        public required List<int> Latches { get; init; }
        public required HashSet<int> Blocks { get; init; }
        public int? Parent { get; set; }
        public int Depth { get; set; }
    }

    readonly record struct TraversalFrame(int Block, int NextTarget);

    public static Avm2LoopAnalysis Analyze(
        int entry_block,
        IReadOnlyList<Avm2BasicBlockInventory> blocks,
        IReadOnlyList<Avm2ControlFlowEdgeInventory> edges)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(edges);

        HashSet<int> block_ids = blocks
            .Select(block => block.Id)
            .ToHashSet();
        if (!block_ids.Contains(entry_block))
            return Empty();

        List<(int Source, int Target)> normal_edges = edges
            .Where(edge =>
                edge.Kind != "Exception" &&
                edge.ToBlock.HasValue &&
                block_ids.Contains(edge.FromBlock) &&
                block_ids.Contains(edge.ToBlock.Value))
            .Select(edge => (edge.FromBlock, edge.ToBlock!.Value))
            .Distinct()
            .OrderBy(edge => edge.FromBlock)
            .ThenBy(edge => edge.Value)
            .Select(edge => (edge.FromBlock, edge.Value))
            .ToList();

        Dictionary<int, List<int>> outgoing = block_ids
            .Order()
            .ToDictionary(block => block, _ => new List<int>());
        Dictionary<int, List<int>> incoming = block_ids
            .Order()
            .ToDictionary(block => block, _ => new List<int>());
        foreach ((int source, int target) in normal_edges)
        {
            outgoing[source].Add(target);
            incoming[target].Add(source);
        }

        HashSet<int> reachable = Reachable(entry_block, outgoing);
        foreach (int block in block_ids)
        {
            outgoing[block] = outgoing[block]
                .Where(reachable.Contains)
                .Distinct()
                .Order()
                .ToList();
            incoming[block] = incoming[block]
                .Where(reachable.Contains)
                .Distinct()
                .Order()
                .ToList();
        }

        List<int> postorder = Postorder(entry_block, outgoing, reachable);
        List<int> reverse_postorder = postorder
            .AsEnumerable()
            .Reverse()
            .ToList();
        Dictionary<int, int> immediate_dominators = ImmediateDominators(
            entry_block,
            reverse_postorder,
            incoming);
        Dictionary<int, List<int>> dominator_chains = DominatorChains(
            entry_block,
            reachable,
            immediate_dominators);
        List<Avm2DominatorInventory> dominators = reachable
            .Order()
            .Select(block => new Avm2DominatorInventory
            {
                Block = block,
                ImmediateDominator = block == entry_block
                    ? null
                    : immediate_dominators.GetValueOrDefault(block),
                Dominators = dominator_chains[block]
            })
            .ToList();

        List<NaturalLoop> loops = NaturalLoops(
            reachable,
            outgoing,
            incoming,
            dominator_chains);
        AssignNesting(loops);
        List<Avm2NaturalLoopInventory> natural_loops = ExportLoops(
            loops,
            outgoing);
        List<Avm2IrreducibleCycleInventory> irreducible_cycles =
            IrreducibleCycles(
                entry_block,
                reachable,
                outgoing,
                incoming,
                postorder,
                dominator_chains);

        return new Avm2LoopAnalysis
        {
            Dominators = dominators,
            NaturalLoops = natural_loops,
            IrreducibleCycles = irreducible_cycles,
            HasLoop = natural_loops.Count > 0 || irreducible_cycles.Count > 0
        };
    }

    static Avm2LoopAnalysis Empty() =>
        new()
        {
            Dominators = [],
            NaturalLoops = [],
            IrreducibleCycles = [],
            HasLoop = false
        };

    static HashSet<int> Reachable(
        int entry,
        IReadOnlyDictionary<int, List<int>> outgoing)
    {
        var reachable = new HashSet<int> { entry };
        var pending = new Queue<int>();
        pending.Enqueue(entry);
        while (pending.TryDequeue(out int source))
        {
            foreach (int target in outgoing[source])
            {
                if (reachable.Add(target))
                    pending.Enqueue(target);
            }
        }
        return reachable;
    }

    static List<int> Postorder(
        int entry,
        IReadOnlyDictionary<int, List<int>> outgoing,
        IReadOnlySet<int> reachable)
    {
        var visited = new HashSet<int> { entry };
        var frames = new List<TraversalFrame>
        {
            new(entry, 0)
        };
        var postorder = new List<int>(reachable.Count);
        while (frames.Count > 0)
        {
            int frame_index = frames.Count - 1;
            TraversalFrame frame = frames[frame_index];
            List<int> targets = outgoing[frame.Block];
            if (frame.NextTarget < targets.Count)
            {
                int target = targets[frame.NextTarget];
                frames[frame_index] = frame with
                {
                    NextTarget = frame.NextTarget + 1
                };
                if (reachable.Contains(target) && visited.Add(target))
                    frames.Add(new TraversalFrame(target, 0));
                continue;
            }
            frames.RemoveAt(frame_index);
            postorder.Add(frame.Block);
        }
        return postorder;
    }

    static Dictionary<int, int> ImmediateDominators(
        int entry,
        IReadOnlyList<int> reverse_postorder,
        IReadOnlyDictionary<int, List<int>> incoming)
    {
        Dictionary<int, int> order = reverse_postorder
            .Select((block, index) => (block, index))
            .ToDictionary(value => value.block, value => value.index);
        var immediate = new Dictionary<int, int>
        {
            [entry] = entry
        };

        bool changed;
        do
        {
            changed = false;
            foreach (int block in reverse_postorder.Skip(1))
            {
                List<int> predecessors = incoming[block]
                    .Where(immediate.ContainsKey)
                    .OrderBy(predecessor => order[predecessor])
                    .ToList();
                if (predecessors.Count == 0)
                    continue;
                int next = predecessors[0];
                foreach (int predecessor in predecessors.Skip(1))
                    next = Intersect(predecessor, next, immediate, order);
                if (immediate.TryGetValue(block, out int current) &&
                    current == next)
                {
                    continue;
                }
                immediate[block] = next;
                changed = true;
            }
        }
        while (changed);
        return immediate;
    }

    static int Intersect(
        int left,
        int right,
        IReadOnlyDictionary<int, int> immediate,
        IReadOnlyDictionary<int, int> order)
    {
        int left_cursor = left;
        int right_cursor = right;
        while (left_cursor != right_cursor)
        {
            while (order[left_cursor] > order[right_cursor])
                left_cursor = immediate[left_cursor];
            while (order[right_cursor] > order[left_cursor])
                right_cursor = immediate[right_cursor];
        }
        return left_cursor;
    }

    static Dictionary<int, List<int>> DominatorChains(
        int entry,
        IEnumerable<int> reachable,
        IReadOnlyDictionary<int, int> immediate)
    {
        var result = new Dictionary<int, List<int>>();
        foreach (int block in reachable.Order())
        {
            var chain = new List<int> { block };
            int cursor = block;
            while (cursor != entry)
            {
                if (!immediate.TryGetValue(cursor, out int parent) ||
                    parent == cursor)
                {
                    break;
                }
                chain.Add(parent);
                cursor = parent;
            }
            chain.Reverse();
            result[block] = chain;
        }
        return result;
    }

    static List<NaturalLoop> NaturalLoops(
        IReadOnlySet<int> reachable,
        IReadOnlyDictionary<int, List<int>> outgoing,
        IReadOnlyDictionary<int, List<int>> incoming,
        IReadOnlyDictionary<int, List<int>> dominators)
    {
        Dictionary<int, List<int>> latches = reachable
            .SelectMany(source => outgoing[source]
                .Where(target => dominators[source].Contains(target))
                .Select(target => (Source: source, Header: target)))
            .GroupBy(edge => edge.Header)
            .OrderBy(group => group.Key)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(edge => edge.Source)
                    .Distinct()
                    .Order()
                    .ToList());
        var loops = new List<NaturalLoop>(latches.Count);
        foreach ((int header, List<int> loop_latches) in latches)
        {
            var body = new HashSet<int> { header };
            var pending = new Stack<int>();
            foreach (int latch in loop_latches.OrderDescending())
            {
                if (body.Add(latch) && latch != header)
                    pending.Push(latch);
            }
            while (pending.TryPop(out int block))
            {
                foreach (int predecessor in incoming[block].OrderDescending())
                {
                    if (!dominators[predecessor].Contains(header) ||
                        !body.Add(predecessor) ||
                        predecessor == header)
                    {
                        continue;
                    }
                    pending.Push(predecessor);
                }
            }
            loops.Add(new NaturalLoop
            {
                Header = header,
                Latches = loop_latches,
                Blocks = body
            });
        }
        return loops;
    }

    static void AssignNesting(List<NaturalLoop> loops)
    {
        for (int loop_index = 0; loop_index < loops.Count; loop_index++)
        {
            NaturalLoop loop = loops[loop_index];
            loop.Parent = Enumerable.Range(0, loops.Count)
                .Where(candidate =>
                    candidate != loop_index &&
                    loop.Blocks.Count < loops[candidate].Blocks.Count &&
                    loop.Blocks.IsSubsetOf(loops[candidate].Blocks))
                .OrderBy(candidate => loops[candidate].Blocks.Count)
                .ThenBy(candidate => loops[candidate].Header)
                .Cast<int?>()
                .FirstOrDefault();
        }
        for (int loop_index = 0; loop_index < loops.Count; loop_index++)
        {
            int depth = 0;
            int? parent = loops[loop_index].Parent;
            while (parent.HasValue)
            {
                depth++;
                parent = loops[parent.Value].Parent;
            }
            loops[loop_index].Depth = depth;
        }
    }

    static List<Avm2NaturalLoopInventory> ExportLoops(
        IReadOnlyList<NaturalLoop> loops,
        IReadOnlyDictionary<int, List<int>> outgoing)
    {
        List<int> order = Enumerable.Range(0, loops.Count)
            .OrderBy(index => loops[index].Depth)
            .ThenBy(index => loops[index].Header)
            .ThenBy(index => loops[index].Blocks.Count)
            .ToList();
        Dictionary<int, int> id_by_index = order
            .Select((index, id) => (index, id))
            .ToDictionary(value => value.index, value => value.id);
        var result = new List<Avm2NaturalLoopInventory>(loops.Count);
        foreach (int loop_index in order)
        {
            NaturalLoop loop = loops[loop_index];
            List<int> ancestry = [];
            int? parent = loop.Parent;
            while (parent.HasValue)
            {
                ancestry.Add(id_by_index[parent.Value]);
                parent = loops[parent.Value].Parent;
            }
            ancestry.Reverse();
            List<int> exiting = loop.Blocks
                .Where(source => outgoing[source].Any(target =>
                    !loop.Blocks.Contains(target)))
                .Order()
                .ToList();
            List<int> exits = loop.Blocks
                .SelectMany(source => outgoing[source])
                .Where(target => !loop.Blocks.Contains(target))
                .Distinct()
                .Order()
                .ToList();
            result.Add(new Avm2NaturalLoopInventory
            {
                Id = id_by_index[loop_index],
                HeaderBlock = loop.Header,
                LatchBlocks = loop.Latches,
                Blocks = loop.Blocks.Order().ToList(),
                ExitingBlocks = exiting,
                ExitBlocks = exits,
                ParentLoop = loop.Parent.HasValue
                    ? id_by_index[loop.Parent.Value]
                    : null,
                Depth = loop.Depth,
                Ancestors = ancestry
            });
        }
        return result;
    }

    static List<Avm2IrreducibleCycleInventory> IrreducibleCycles(
        int entry,
        IReadOnlySet<int> reachable,
        IReadOnlyDictionary<int, List<int>> outgoing,
        IReadOnlyDictionary<int, List<int>> incoming,
        IReadOnlyList<int> postorder,
        IReadOnlyDictionary<int, List<int>> dominators)
    {
        var assigned = new HashSet<int>();
        var components = new List<List<int>>();
        foreach (int root in postorder.AsEnumerable().Reverse())
        {
            if (!assigned.Add(root))
                continue;
            var component = new List<int>();
            var pending = new Stack<int>();
            pending.Push(root);
            while (pending.TryPop(out int block))
            {
                component.Add(block);
                foreach (int predecessor in incoming[block].OrderDescending())
                {
                    if (reachable.Contains(predecessor) &&
                        assigned.Add(predecessor))
                    {
                        pending.Push(predecessor);
                    }
                }
            }
            component.Sort();
            components.Add(component);
        }

        var irreducible = new List<(List<int> Blocks, List<int> Entries)>();
        foreach (List<int> component in components
            .OrderBy(component => component[0]))
        {
            bool cyclic = component.Count > 1 ||
                outgoing[component[0]].Contains(component[0]);
            if (!cyclic)
                continue;
            HashSet<int> members = component.ToHashSet();
            List<int> entries = component
                .Where(block =>
                    block == entry ||
                    incoming[block].Any(predecessor =>
                        !members.Contains(predecessor)))
                .Distinct()
                .Order()
                .ToList();
            bool single_header = entries.Count == 1 &&
                component.All(block =>
                    dominators[block].Contains(entries[0]));
            if (!single_header)
                irreducible.Add((component, entries));
        }

        return irreducible
            .Select((component, id) => new Avm2IrreducibleCycleInventory
            {
                Id = id,
                Blocks = component.Blocks,
                EntryBlocks = component.Entries
            })
            .ToList();
    }
}
