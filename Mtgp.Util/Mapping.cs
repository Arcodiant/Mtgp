using System.Collections;

namespace Mtgp.Util;

public class Mapping<TLeft, TRight>
	: IEnumerable<KeyValuePair<TLeft, TRight>>
	where TLeft : notnull
	where TRight : notnull
{
	private readonly Dictionary<TLeft, TRight> leftToRight = [];
	private readonly Dictionary<TRight, TLeft> rightToLeft = [];

	public Mapping()
	{
	}

	public void Add(KeyValuePair<TLeft, TRight> pair)
		=> Add(pair.Key, pair.Value);

	public void Add(TLeft left, TRight right)
	{
		if (leftToRight.ContainsKey(left) || rightToLeft.ContainsKey(right))
		{
			throw new InvalidOperationException("Mapping already contains this key.");
		}

		leftToRight[left] = right;
		rightToLeft[right] = left;
	}

	public bool Remove(TLeft left)
	{
		if (leftToRight.TryGetValue(left, out var right))
		{
			leftToRight.Remove(left);
			rightToLeft.Remove(right);

			return true;
		}

		return false;
	}

	public IEnumerator<KeyValuePair<TLeft, TRight>> GetEnumerator()
		=> leftToRight.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator()
		=> this.GetEnumerator();

	public TRight this[TLeft key]
	{
		get
		{
			if (!leftToRight.TryGetValue(key, out var value))
			{
				throw new KeyNotFoundException($"Key '{key}' not found in mapping.");
			}

			return value;
		}
		set
		{
			if (leftToRight.TryGetValue(key, out var rightValue))
			{
				rightToLeft.Remove(rightValue);
			}

			if (rightToLeft.TryGetValue(value, out var leftKey))
			{
				leftToRight.Remove(leftKey);
			}

			leftToRight[key] = value;
			rightToLeft[value] = key;
		}
	}

	public IReadOnlyDictionary<TLeft, TRight> LeftToRight => leftToRight;

	public IReadOnlyDictionary<TRight, TLeft> RightToLeft => rightToLeft;
}
