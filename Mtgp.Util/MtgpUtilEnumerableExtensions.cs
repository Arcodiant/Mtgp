namespace Mtgp;

public static class MtgpUtilEnumerableExtensions
{
	public static IEnumerable<int> RunningOffset(this IEnumerable<int> enumerable)
	{
		int offset = 0;

		foreach (var value in enumerable)
		{
			yield return offset;

			offset += value;
		}
	}
}
