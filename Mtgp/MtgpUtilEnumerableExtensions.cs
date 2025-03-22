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

	public static IEnumerable<(int Id, int Offset)> RunningOffset(this IEnumerable<(int Id, int Offset)> enumerable)
	{
		int offset = 0;

		foreach (var (id, value) in enumerable)
		{
			yield return (id, offset);

			offset += value;
		}
	}
}
