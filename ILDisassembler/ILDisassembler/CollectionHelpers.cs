using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILDisassembler
{
	/// <summary>
	/// Constains helper methods for collections
	/// </summary>
	internal static class CollectionHelpers
	{
		/// <summary>
		/// Addds a mapping for the given key to value in a multi-value dictionary
		/// </summary>
		/// <typeparam name="TKey">The type of the key</typeparam>
		/// <typeparam name="TValue">The type of the value</typeparam>
		/// <param name="dict">The dictionary</param>
		/// <param name="key">The key</param>
		/// <param name="value">The value</param>
		public static void AddMulti<TKey, TValue>(this IDictionary<TKey, IList<TValue>> dict, TKey key, TValue value)
		{
			if (dict.ContainsKey(key))
			{
				dict[key].Add(value);
			}
			else
			{
				var list = new List<TValue>();
				dict.Add(key, list);
				list.Add(value);
			}
		}
	}
}
