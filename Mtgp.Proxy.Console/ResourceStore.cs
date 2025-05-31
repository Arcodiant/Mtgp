using Mtgp.Proxy.Shader;

namespace Mtgp.Proxy;


internal class ResourceStore
{
	private readonly Dictionary<Type, int> nextIds = [];
	private readonly Dictionary<Type, object> stores = [];
	private record ResourceKey(string ResourceType, int Id);
	private readonly Dictionary<ResourceKey, HashSet<ResourceKey>> resourceReferences = [];
	private readonly Dictionary<ResourceKey, HashSet<ResourceKey>> resourceBackReferences = [];

	private Dictionary<int, T> GetStore<T>()
		where T : IShaderProxyResource
	{
		if (!this.stores.TryGetValue(typeof(T), out var store))
		{
			store = new Dictionary<int, T>();
			this.stores[typeof(T)] = store;
		}

		return (Dictionary<int, T>)store;
	}

	public int Add<T>(T item)
		where T : IShaderProxyResource
	{
		var store = this.GetStore<T>();

		if (!this.nextIds.TryGetValue(typeof(T), out var nextId))
		{
			nextId = 0;
		}

		this.nextIds[typeof(T)] = nextId + 1;

		store.Add(nextId, item);

		var key = new ResourceKey(T.ResourceType, nextId);

		this.resourceReferences[key] = [];
		this.resourceBackReferences[key] = [];

		return nextId;
	}

	public T Get<T>(int index)
		where T : IShaderProxyResource
	{
		T? value = this.GetStore<T>()[index];

		return value is not null ? value : throw new InvalidOperationException();
	}

	public T[] Get<T>(int[] indices)
		where T : IShaderProxyResource
	{
		return [.. indices.Select(Get<T>)];
	}

	public V[] Get<T, V>(int[] indices, Func<T?, V> selector)
		where T : IShaderProxyResource
	{
		return [.. indices.Select(Get<T>).Select(selector)];
	}

	public bool CanRemove<T>(int index)
		where T : IShaderProxyResource
		=> this.CanRemove(T.ResourceType, index);

	public bool CanRemove(string resourceType, int index)
		=> !this.resourceReferences.TryGetValue(new(resourceType, index), out var references) || references.Count <= 0;

	public void Remove<T>(int index)
		where T : IShaderProxyResource
	{
		if (!this.CanRemove<T>(index))
		{
			throw new InvalidOperationException($"Cannot remove resource {typeof(T).Name} {index} because it is referenced by other resources.");
		}

		this.ClearReferences<T>(index);

		this.resourceBackReferences.Remove(new ResourceKey(T.ResourceType, index));

		this.GetStore<T>().Remove(index);
	}

	public void ClearReferences<T>(int index)
		where T : IShaderProxyResource
	{
		var key = new ResourceKey(T.ResourceType, index);

		foreach (var reference in this.resourceBackReferences[key])
		{
			this.resourceReferences[reference].Remove(key);
		}

		this.resourceBackReferences[key].Clear();
	}

	public void AddReference<TFrom, TTo>(int index, int referenceIndex)
		where TFrom : IShaderProxyResource
		where TTo : IShaderProxyResource
	{
		var fromKey = new ResourceKey(TFrom.ResourceType, index);
		var toKey = new ResourceKey(TTo.ResourceType, referenceIndex);

		this.resourceReferences[toKey].Add(fromKey);
		this.resourceBackReferences[fromKey].Add(toKey);
	}

	public void AddReferences<TFrom, TTo>(int index, IEnumerable<int> referenceIndices)
		where TFrom : IShaderProxyResource
		where TTo : IShaderProxyResource
	{
		foreach (var referenceIndex in referenceIndices)
		{
			this.AddReference<TFrom, TTo>(index, referenceIndex);
		}
	}
}
