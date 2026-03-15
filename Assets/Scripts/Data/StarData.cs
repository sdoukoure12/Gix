using UnityEngine;

namespace Gix.Data
{
    /// <summary>
    /// Represents a single star from the catalog.
    /// </summary>
    [System.Serializable]
    public struct StarData
    {
        /// <summary>Hipparcos catalog identifier.</summary>
        public int HipId;

        /// <summary>Common name (e.g. "Sirius"), empty if unnamed.</summary>
        public string Name;

        /// <summary>Right Ascension in degrees (0–360).</summary>
        public float RightAscension;

        /// <summary>Declination in degrees (-90 to +90).</summary>
        public float Declination;

        /// <summary>Apparent visual magnitude (lower = brighter).</summary>
        public float Magnitude;

        /// <summary>B-V color index used to compute star color.</summary>
        public float ColorIndex;

        /// <summary>Spectral type string (e.g. "G2V").</summary>
        public string SpectralType;

        /// <summary>Distance in light-years.</summary>
        public float DistanceLY;

        /// <summary>
        /// Returns the LOD level for this star based on its magnitude.
        /// 0 = bright (mag &lt; 3): detailed sprite + label
        /// 1 = medium (mag 3–6): simple point
        /// 2 = faint (mag &gt; 6): not rendered in AR
        /// </summary>
        public int GetLODLevel()
        {
            if (Magnitude < 3f) return 0;
            if (Magnitude <= 6f) return 1;
            return 2;
        }

        /// <summary>Returns a Color approximation based on B-V color index.</summary>
        public Color GetColor()
        {
            // B-V index ranges from ~-0.4 (blue-white) to ~2.0 (deep red)
            float t = Mathf.InverseLerp(-0.4f, 2.0f, ColorIndex);
            return Color.Lerp(new Color(0.6f, 0.7f, 1.0f), new Color(1.0f, 0.4f, 0.1f), t);
        }
    }

    /// <summary>
    /// Represents a constellation as an ordered list of star HIP IDs connected by line segments.
    /// </summary>
    [System.Serializable]
    public struct ConstellationData
    {
        /// <summary>IAU abbreviation (e.g. "ORI" for Orion).</summary>
        public string Abbreviation;

        /// <summary>Full name (e.g. "Orion").</summary>
        public string Name;

        /// <summary>
        /// Pairs of HIP IDs defining line segments.
        /// Each consecutive pair [2i, 2i+1] forms one line.
        /// </summary>
        public int[] LinePairs;
    }

    /// <summary>
    /// Represents a satellite with its Two-Line Element (TLE) data.
    /// </summary>
    [System.Serializable]
    public class SatelliteData
    {
        /// <summary>NORAD catalog number.</summary>
        public int NoradId;

        /// <summary>Satellite name (e.g. "ISS (ZARYA)").</summary>
        public string Name;

        /// <summary>First line of TLE.</summary>
        public string TleLine1;

        /// <summary>Second line of TLE.</summary>
        public string TleLine2;

        /// <summary>Satellite category (e.g. Station, Weather, Navigation, Debris).</summary>
        public SatelliteCategory Category;

        /// <summary>UTC epoch when the TLE was issued.</summary>
        public System.DateTime TleEpoch;

        /// <summary>Whether this satellite is available in the free tier.</summary>
        public bool IsFree;

        public override string ToString() => $"{Name} (NORAD #{NoradId})";
    }

    public enum SatelliteCategory
    {
        Station,
        Weather,
        Navigation,
        Communications,
        Scientific,
        Debris,
        Other
    }

    /// <summary>
    /// Computed position of a celestial object in the local horizontal coordinate system.
    /// </summary>
    public struct HorizontalCoordinates
    {
        /// <summary>Azimuth in degrees measured clockwise from North (0–360).</summary>
        public double Azimuth;

        /// <summary>Altitude (elevation) above horizon in degrees (-90 to +90).</summary>
        public double Altitude;

        /// <summary>Returns true when the object is above the horizon.</summary>
        public bool IsAboveHorizon => Altitude > 0.0;
    }

    /// <summary>
    /// Observer location on Earth.
    /// </summary>
    public struct ObserverLocation
    {
        /// <summary>Geodetic latitude in degrees.</summary>
        public double Latitude;

        /// <summary>Geodetic longitude in degrees (East positive).</summary>
        public double Longitude;

        /// <summary>Altitude above sea level in metres.</summary>
        public double AltitudeMetres;
    }
}
