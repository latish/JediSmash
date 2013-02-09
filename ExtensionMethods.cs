using System;
using Kinect.Toolbox;

namespace Kinect9.JediSmash
{
	public static class ExtensionMethods
	{
		public static Vector3 Normalize(this Vector3 value)
		{
			var num = 1f / (float)Math.Sqrt((double)value.X * (double)value.X + (double)value.Y * (double)value.Y + (double)value.Z * (double)value.Z);
			Vector3 vector3;
			vector3.X = value.X * num;
			vector3.Y = value.Y * num;
			vector3.Z = value.Z * num;
			return vector3;
		}

		/// <summary>
		/// Calculates the cross product of two vectors.
		/// </summary>
		/// <param name="vector1">Source vector.</param><param name="vector2">Source vector.</param>
		public static Vector3 Cross(this Vector3 vector1, Vector3 vector2)
		{
			Vector3 vector3;
			vector3.X = (float)((double)vector1.Y * (double)vector2.Z - (double)vector1.Z * (double)vector2.Y);
			vector3.Y = (float)((double)vector1.Z * (double)vector2.X - (double)vector1.X * (double)vector2.Z);
			vector3.Z = (float)((double)vector1.X * (double)vector2.Y - (double)vector1.Y * (double)vector2.X);
			return vector3;
		}

	}
}
