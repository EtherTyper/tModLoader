﻿using System;
using System.Runtime.CompilerServices;

namespace HookListPerformance
{
	class StructEnumeratorInstancedField : Implementation
	{
		// Don't change a single line without performance testing and checking the disassembly. As of NET 5.0.0, this implementation is on par with hand-coding
		// Disassembly checked using Relyze Desktop 3.3.0
		ref struct InstanceEnumerator<T>
		{
			// These have to be arrays rather than ReadOnlySpan as the JIT won't unpack/promote 'struct in struct' (or span in struct either)
			// Revisit with .NET 6 https://github.com/dotnet/runtime/issues/37924
			private readonly Instanced<T>[] instances;
			private readonly int[] hookInds;

			private Instanced<T> current;
			// i and j are combined into a 64bit variable beacuse JIT currently won't unpack/promote structs with > 4 fields into registers
			// See https://github.com/dotnet/runtime/issues/6534
			private ulong ij;
			//private int i;
			//private int j;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public InstanceEnumerator(Instanced<T>[] instances, int[] hookInds) {
				this.instances = instances;
				this.hookInds = hookInds;
				current = new Instanced<T>(default, -1);
				ij = 0;
				//i = 0;
				//j = 0;
			}

			public T Current => current.instance;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext() {
				while ((int)(ij >> 32) < hookInds.Length) {
					int hookIndex = hookInds[(int)(ij >> 32)];
					ij += 1L << 32;
					while (current.index < hookIndex) {
						if ((int)ij == instances.Length)
							return false;

						current = instances[(int)(ij++)];
					}

					if (current.index == hookIndex) {
						return true;
					}
				}

				return false;
			}

			public InstanceEnumerator<T> GetEnumerator() => this;
		}

		private static float HookDoEffect(ref float input, Instanced<GlobalItem>[] instances, int[] hookInds) {
			float result = 0;
			foreach (var globalItem in new InstanceEnumerator<GlobalItem>(instances, hookInds))
				result += globalItem.DoEffect(ref input);

			return result;
		}

		public override float HookDoEffect(float input, Item[] items, int[] hookInds) {
			float result = 0;
			foreach (var item in items)
				result += HookDoEffect(ref input, item.instancedGlobals, hookInds);
			return result;
		}
	}
}