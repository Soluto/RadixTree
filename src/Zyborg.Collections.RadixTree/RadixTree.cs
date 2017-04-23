using System;
using System.Collections.Generic;
using System.IO;

namespace Zyborg.Collections
{
    /// <summary>
    /// Walker is used when walking the tree. Takes a key and value, returning if iteration should be terminated.
    /// </summary>
    public delegate bool Walker<in TValue>(string key, TValue value);

    /// <summary>
    /// used to represent a value
    /// </summary>
    internal class LeafNode<TValue>
    {
        internal string Key = string.Empty;
        internal TValue Value;
    }

    internal class Node<TValue>
    {
        /// <summary>
        /// used to store possible leaf
        /// </summary>
        internal LeafNode<TValue> Leaf;
        /// <summary>
        /// the common prefix we ignore
        /// </summary>
        internal string Prefix = string.Empty;
        /// <summary>
        /// Edges should be stored in-order for iteration.
        /// We avoid a fully materialized slice to save memory,
        /// since in most cases we expect to be sparse
        /// </summary>
        internal SortedList<char, Node<TValue>> Edges = new SortedList<char, Node<TValue>>();

        //~ func (n *node) isLeaf() bool {
        //~ 	return n.leaf != nil
        //~ }
        public bool IsLeaf => Leaf != null;

        //~ func (n *node) addEdge(e edge) {
        //~ 	n.edges = append(n.edges, e)
        //~ 	n.edges.Sort()
        //~ }
        public void AddEdge(char label, Node<TValue> node)
        {
            Edges.Add(label, node);
        }

        //~ func (n *node) replaceEdge(e edge) {
        //~ 	num := len(n.edges)
        //~ 	idx := sort.Search(num, func(i int) bool {
        //~ 		return n.edges[i].label >= e.label
        //~ 	})
        //~ 	if idx < num && n.edges[idx].label == e.label {
        //~ 		n.edges[idx].node = e.node
        //~ 		return
        //~ 	}
        //~ 	panic("replacing missing edge")
        //~ }
        public void ReplaceEdge(char label, Node<TValue> node)
        {
            if (!Edges.ContainsKey(label)) throw new Exception("replacing missing edge");
            Edges[label] = node;
        }

        //~ func (n *node) getEdge(label byte) *node {
        //~ 	num := len(n.edges)
        //~ 	idx := sort.Search(num, func(i int) bool {
        //~ 		return n.edges[i].label >= label
        //~ 	})
        //~ 	if idx < num && n.edges[idx].label == label {
        //~ 		return n.edges[idx].node
        //~ 	}
        //~ 	return nil
        //~ }
        public Node<TValue> GetEdge(char label)
        {
          return Edges.TryGetValue(label, out var edge) ? edge : null;
        } 

        //~ func (n *node) delEdge(label byte) {
        //~ 	num := len(n.edges)
        //~ 	idx := sort.Search(num, func(i int) bool {
        //~ 		return n.edges[i].label >= label
        //~ 	})
        //~ 	if idx < num && n.edges[idx].label == label {
        //~ 		copy(n.edges[idx:], n.edges[idx+1:])
        //~ 		n.edges[len(n.edges)-1] = edge{}
        //~ 		n.edges = n.edges[:len(n.edges)-1]
        //~ 	}
        //~ }
        public void RemoveEdge(char label)
        {
            Edges.Remove(label);
        }

        //~ func (n *node) mergeChild() {
        //~ 	e := n.edges[0]
        //~ 	child := e.node
        //~ 	n.prefix = n.prefix + child.prefix
        //~ 	n.leaf = child.leaf
        //~ 	n.edges = child.edges
        //~ }
        public void MergeChild()
        {
            var child = Edges.Values[0];

            Prefix = Prefix + child.Prefix;
            Leaf = child.Leaf;
            Edges = child.Edges;
        }

        public void Print(StreamWriter w, string prefix)
        {
            w.WriteLine($"{prefix}Prfx=[{Prefix}]");
            if (Leaf != null)
                w.WriteLine($"{prefix}Leaf:  [{Leaf.Key}] = [{Leaf.Value}]");
            foreach (var e in Edges)
            {
                w.WriteLine($"{prefix}Edge:  label=[{e.Key}]");
                e.Value.Print(w, prefix + "  ");
            }
        }
    }

    /// <summary>
    /// Tree implements a radix tree. This can be treated as a 
    /// Dictionary abstract data type. The main advantage over
    /// a standard hash map is prefix-based lookups and
    /// ordered iteration,
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class RadixTree<TValue>
    {
        private readonly Node<TValue> _root;
        private int _size;

        /// <summary>
        /// Initializes a new instance of the <see cref="RadixTree{TValue}"/> class.
        /// returns an empty Tree
        /// </summary>
        public RadixTree()
            : this(null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RadixTree{TValue}"/> class.
        /// returns a new tree containing the keys from an existing map
        /// </summary>
        /// <param name="map"></param>
        public RadixTree(IReadOnlyDictionary<string, TValue> map)
        {
            _root = new Node<TValue>();
            if (map != null)
            {
                foreach (var kv in map)
                {
                    GoInsert(kv.Key, kv.Value);
                }
            }
        }

        /// <summary>
        /// number of elements in the tree
        /// </summary>
        public int Count => _size;

        /// <summary>
        /// finds the length of the shared prefix of two strings
        /// </summary>
        /// <param name="str1">first string to compare</param>
        /// <param name="str2">secont string to compare</param>
        /// <returns>length of shared prefix</returns>
        public static int FindLongestPrefix(string str1, string str2)
        {
            //	max := len(k1)
            //	if l := len(k2); l < max {
            //		max = l
            //	}
            //	var i int
            //	for i = 0; i < max; i++ {
            //		if k1[i] != k2[i] {
            //			break
            //		}
            //	}
            //	return i

            var max = str1.Length > str2.Length ? str2.Length : str1.Length;

            for (var i = 0; i < max; i++)
                if (str1[i] != str2[i])
                    return i;

            return max;
        }

        /// <summary>
        /// adds new entry or updates an existing entry
        /// </summary>
        /// <returns>is entry updated, and old value if it was</returns>
        public (TValue oldValue, bool updated) GoInsert(string key, TValue value)
        {
            //~ var parent *node
            //~ n := t.root
            //~ search := s
            var n = _root;
            var search = key;

            //~ for {
            while (true)
            {
                // Handle key exhaution
                //~ if len(search) == 0 {
                //~ 	if n.isLeaf() {
                //~ 		old := n.leaf.val
                //~ 		n.leaf.val = v
                //~ 		return old, true
                //~ 	}
                //~
                //~ 	n.leaf = &leafNode{
                //~ 		key: s,
                //~ 		val: v,
                //~ 	}
                //~ 	t.size++
                //~ 	return nil, false
                //~ }
                if (search.Length == 0)
                {
                    if (n.IsLeaf)
                    {
                        var old = n.Leaf.Value;
                        n.Leaf.Value = value;
                        return (old, true);
                    }

                    n.Leaf = new LeafNode<TValue>
                    {
                        Key = key,
                        Value = value,
                    };
                    _size++;
                    return (default(TValue), false);
                }

                // Look for the edge
                //~ parent = n
                //~ n = n.getEdge(search[0])
                var parent = n;
                n = n.GetEdge(search[0]);

                // No edge, create one
                //~ if n == nil {
                //~ 	e := edge{
                //~ 		label: search[0],
                //~ 		node: &node{
                //~ 			leaf: &leafNode{
                //~ 				key: s,
                //~ 				val: v,
                //~ 			},
                //~ 			prefix: search,
                //~ 		},
                //~ 	}
                //~ 	parent.addEdge(e)
                //~ 	t.size++
                //~ 	return nil, false
                //~ }
                if (n == null)
                {
                    parent.AddEdge(search[0], new Node<TValue>
                    {
                        Leaf = new LeafNode<TValue>
                        {
                            Key = key,
                            Value = value,
                        },
                        Prefix = search,
                    });
                    _size++;
                    return (default(TValue), false);
                }

                // Determine longest prefix of the search key on match
                //~ commonPrefix := longestPrefix(search, n.prefix)
                //~ if commonPrefix == len(n.prefix) {
                //~ 	search = search[commonPrefix:]
                //~ 	continue
                //~ }
                var commonPrefix = FindLongestPrefix(search, n.Prefix);
                if (commonPrefix == n.Prefix.Length)
                {
                    search = search.Substring(commonPrefix);
                    continue;
                }

                // Split the node
                //~ t.size++
                //~ child := &node{
                //~ 	prefix: search[:commonPrefix],
                //~ }
                //~ parent.replaceEdge(edge{
                //~ 	label: search[0],
                //~ 	node:  child,
                //~ })
                _size++;
                var child = new Node<TValue>
                {
                    Prefix = search.Substring(0, commonPrefix),
                };
                parent.ReplaceEdge(search[0], child);

                // Restore the existing node
                //~ child.addEdge(edge{
                //~ 	label: n.prefix[commonPrefix],
                //~ 	node:  n,
                //~ })
                //~ n.prefix = n.prefix[commonPrefix:]
                child.AddEdge(n.Prefix[commonPrefix], n);
                n.Prefix = n.Prefix.Substring(commonPrefix);

                // Create a new leaf node
                //~ leaf := &leafNode{
                //~ 	key: s,
                //~ 	val: v,
                //~ }
                var leaf = new LeafNode<TValue>
                {
                    Key = key,
                    Value = value,
                };

                // If the new key is a subset, add to to this node
                //~ search = search[commonPrefix:]
                //~ if len(search) == 0 {
                //~ 	child.leaf = leaf
                //~ 	return nil, false
                //~ }
                search = search.Substring(commonPrefix);
                if (search.Length == 0)
                {
                    child.Leaf = leaf;
                    return (default(TValue), false);
                }

                // Create a new edge for the node
                //~ child.addEdge(edge{
                //~ 	label: search[0],
                //~ 	node: &node{
                //~ 		leaf:   leaf,
                //~ 		prefix: search,
                //~ 	},
                //~ })
                //~ return nil, false
                child.AddEdge(search[0], new Node<TValue>
                {
                    Leaf = leaf,
                    Prefix = search,
                });
                return (default(TValue), false);
            }
        }

        /// <summary>
        /// deletes a key
        /// </summary>
        /// <returns>is entry deleted, and the value what was deleted</returns>
        public (TValue oldValue, bool deleted) GoDelete(string key)
        {
            //~	var parent *node
            //~	var label byte
            //~	n := t.root
            //~	search := s
            Node<TValue> parent = null;
            char label = char.MinValue;
            var n = _root;
            var search = key;

            //!	for {
            while (true)
            {
                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	if !n.isLeaf() {
                //~ 		break
                //~ 	}
                //~ 	goto DELETE
                //~ }
                if (search.Length == 0)
                {
                    if (!n.IsLeaf)
                        break;

                    goto DELETE;
                }

                // Look for an edge
                //~ parent = n
                //~ label = search[0]
                //~ n = n.getEdge(label)
                //~ if n == nil {
                //~ 	break
                //~ }
                parent = n;
                label = search[0];
                n = n.GetEdge(label);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n.Prefix))
                    search = search.Substring(n.Prefix.Length);
                else
                    break;
            }

            //~	return nil, false
            return (default(TValue), false);

            DELETE:
            // Delete the leaf
            //~	leaf := n.leaf
            //~	n.leaf = nil
            //~	t.size--
            var leaf = n.Leaf;
            n.Leaf = null;
            _size--;

            // Check if we should delete this node from the parent
            //~	if parent != nil && len(n.edges) == 0 {
            //~		parent.delEdge(label)
            //~	}
            if (parent != null && n.Edges.Count == 0)
                parent.RemoveEdge(label);

            // Check if we should merge this node
            //~	if n != t.root && len(n.edges) == 1 {
            //~		n.mergeChild()
            //~	}
            if (n != _root && n.Edges.Count == 1)
                n.MergeChild();

            // Check if we should merge the parent's other child
            //~	if parent != nil && parent != t.root && len(parent.edges) == 1 && !parent.isLeaf() {
            //~		parent.mergeChild()
            //~	}
            if (parent != null && parent != _root && parent.Edges.Count == 1 && !parent.IsLeaf)
                parent.MergeChild();

            //~ return leaf.val, true
            return (leaf.Value, true);
        }

        /// <summary>
        /// lookup a specific key
        /// </summary>
        /// <returns>the value and if it was found</returns>
        public (TValue value, bool found) GoGet(string key)
        {
            //	n := t.root
            //	search := s
            var n = _root;
            var search = key;

            //~ for {
            while (true)
            {
                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	if n.isLeaf() {
                //~ 		return n.leaf.val, true
                //~ 	}
                //~ 	break
                //~ }
                if (search.Length == 0)
                {
                    if (n.IsLeaf)
                        return (n.Leaf.Value, true);
                    break;
                }

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	break
                //~ }
                n = n.GetEdge(search[0]);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n.Prefix))
                    search = search.Substring(n.Prefix.Length);
                else
                    break;
            }

            //~	return nil, false
            return (default(TValue), false);
        }

        /// <summary>
        /// searches for the longest prefix match
        /// </summary>
        public (string key, TValue value, bool found) LongestPrefix(string prefix)
        {
            //~ var last *leafNode
            //~ n := t.root
            //~ search := s
            LeafNode<TValue> last = null;
            var n = _root;
            var search = prefix;

            //for {
            while (true)
            {
                // Look for a leaf node
                //~ if n.isLeaf() {
                //~ 	last = n.leaf
                //~ }
                if (n.IsLeaf)
                    last = n.Leaf;

                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	break
                //~ }
                if (search.Length == 0)
                    break;

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	break
                //~ }
                n = n.GetEdge(search[0]);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n.Prefix))
                    search = search.Substring(n.Prefix.Length);
                else
                    break;
            }

            //~ if last != nil {
            //~ 	return last.key, last.val, true
            //~ }
            //~ return "", nil, false

            if (last != null)
                return (last.Key, last.Value, true);
            return (string.Empty, default(TValue), false);
        }

        /// <summary>
        /// returns the minimum value in the tree
        /// </summary>
        public (string key, TValue value, bool found) Minimum()
        {
            //!n := t.root
            var n = _root;

            //for {
            //	if n.isLeaf() {
            //		return n.leaf.key, n.leaf.val, true
            //	}
            //	if len(n.edges) > 0 {
            //		n = n.edges[0].node
            //	} else {
            //		break
            //	}
            //}
            while (true)
            {
                if (n.IsLeaf)
                    return (n.Leaf.Key, n.Leaf.Value, true);

                if (n.Edges.Count > 0)
                    n = n.Edges.Values[0];
                else
                    break;
            }

            //return "", nil, false
            return (string.Empty, default(TValue), false);
        }

        /// <summary>
        /// returns the maximum value in the tree
        /// </summary>
        /// <returns></returns>
        public (string key, TValue value, bool found) Maximum()
        {
            //~ n := t.root
            var n = _root;

            //for {
            //	if num := len(n.edges); num > 0 {
            //		n = n.edges[num-1].node
            //		continue
            //	}
            //	if n.isLeaf() {
            //		return n.leaf.key, n.leaf.val, true
            //	}
            //	break
            //}
            while (true)
            {
                var num = n.Edges.Count;
                if (num > 0)
                {
                    n = n.Edges.Values[num - 1];
                    continue;
                }
                if (n.IsLeaf)
                    return (n.Leaf.Key, n.Leaf.Value, true);

                break;
            }

            //return "", nil, false
            return (string.Empty, default(TValue), false);
        }

        // Walk is used to walk the tree
        //~ func (t *Tree) Walk(fn WalkFn) {
        public void Walk(Walker<TValue> fn)
        {
            RecursiveWalk(_root, fn);
        }


        // WalkPrefix is used to walk the tree under a prefix
        //~ func (t *Tree) WalkPrefix(prefix string, fn WalkFn) {
        public void WalkPrefix(string prefix, Walker<TValue> fn)
        {
            //~ n := t.root
            //~ search := prefix
            var n = _root;
            var search = prefix;

            //~ for {
            while (true)
            {
                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	recursiveWalk(n, fn)
                //~ 	return
                //~ }
                if (search.Length == 0)
                {
                    RecursiveWalk(n, fn);
                    return;
                }

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	break
                //~ }
                n = n.GetEdge(search[0]);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ 
                //~ } else if strings.HasPrefix(n.prefix, search) {
                //~ 	// Child may be under our search prefix
                //~ 	recursiveWalk(n, fn)
                //~ 	return
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n.Prefix))
                {
                    search = search.Substring(n.Prefix.Length);
                }
                else if (n.Prefix.StartsWith(search))
                {
                    RecursiveWalk(n, fn);
                    return;
                }
                else
                {
                    break;
                }
            }
        }

        // WalkPath is used to walk the tree, but only visiting nodes
        // from the root down to a given leaf. Where WalkPrefix walks
        // all the entries *under* the given prefix, this walks the
        // entries *above* the given prefix.
        //~ func (t *Tree) WalkPath(path string, fn WalkFn) {
        public void WalkPath(string path, Walker<TValue> fn)
        {
            //~ n := t.root
            //~ search := path
            var n = _root;
            var search = path;

            //~ for {
            while (true)
            {
                // Visit the leaf values if any
                //~ if n.leaf != nil && fn(n.leaf.key, n.leaf.val) {
                //~ 	return
                //~ }
                if (n.Leaf != null && fn(n.Leaf.Key, n.Leaf.Value))
                    return;

                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	return
                //~ }
                if (search.Length == 0)
                    return;

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	return
                //~ }
                n = n.GetEdge(search[0]);
                if (n == null)
                    return;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n.Prefix))
                    search = search.Substring(n.Prefix.Length);
                else
                    break;
            }
        }

        // recursiveWalk is used to do a pre-order walk of a node
        // recursively. Returns true if the walk should be aborted
        //~ func recursiveWalk(n *node, fn WalkFn) bool {
        private static bool RecursiveWalk(Node<TValue> n, Walker<TValue> fn)
        {
            // Visit the leaf values if any
            //~ if n.leaf != nil && fn(n.leaf.key, n.leaf.val) {
            //~ 	return true
            //~ }
            if (n.Leaf != null && fn(n.Leaf.Key, n.Leaf.Value))
                return true;

            // Recurse on the children
            //~ for _, e := range n.edges {
            //~ 	if recursiveWalk(e.node, fn) {
            //~ 		return true
            //~ 	}
            //~ }
            foreach (var e in n.Edges.Values)
            {
                if (RecursiveWalk(e, fn))
                    return true;
            }

            //~return false
            return false;
        }

        // ToMap is used to walk the tree and convert it into a map
        //! func (t *Tree) ToMap() map[string]interface{} {
        public IDictionary<string, TValue> ToMap()
        {
            //! out := make(map[string]interface{}, t.size)
            var @out = new Dictionary<string, TValue>(_size);

            //~ t.Walk(func(k string, v interface{}) bool {
            //~ 	out[k] = v
            //~ 	return false
            //~ })
            Walk((k, v) =>
            {
                @out[k] = v;
                return false;
            });

            //~ return out
            return @out;
        }

        public void Print(Stream s)
        {
            using (var sw = new StreamWriter(s))
            {
                _root.Print(sw, "");
            }
        }
    }
}
