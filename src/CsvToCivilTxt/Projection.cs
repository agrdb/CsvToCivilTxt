using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CsvToCivilTxt
{
    /// <summary>Sistema de coordenadas planas destino (Transverse Mercator).</summary>
    public class ProjectedCrs
    {
        public string Name;
        public int Epsg;
        public double A;      // semieje mayor
        public double F;      // achatamiento
        public double Lat0;   // latitud de origen (grados)
        public double Lon0;   // meridiano central (grados)
        public double K0;     // factor de escala
        public double FE;     // falso este
        public double FN;     // falso norte

        public override string ToString() => $"{Name}  (EPSG:{Epsg})";

        private const double GrsA = 6378137.0, GrsF = 1 / 298.257222101;   // GRS80 (MAGNA-SIRGAS)
        private const double WgsA = 6378137.0, WgsF = 1 / 298.257223563;   // WGS84 (UTM)
        private const double MagnaLat0 = 4.596200416666666;                // 4°35'46.3215"N

        public static readonly List<ProjectedCrs> All = new List<ProjectedCrs>
        {
            new ProjectedCrs { Name = "MAGNA-SIRGAS / Origen Nacional (CTM12)", Epsg = 9377, A = GrsA, F = GrsF, Lat0 = 4.0, Lon0 = -73.0, K0 = 0.9992, FE = 5000000, FN = 2000000 },
            new ProjectedCrs { Name = "MAGNA-SIRGAS / Colombia Bogotá",       Epsg = 3116, A = GrsA, F = GrsF, Lat0 = MagnaLat0, Lon0 = -74.07750791666666, K0 = 1, FE = 1000000, FN = 1000000 },
            new ProjectedCrs { Name = "MAGNA-SIRGAS / Colombia Oeste",        Epsg = 3115, A = GrsA, F = GrsF, Lat0 = MagnaLat0, Lon0 = -77.07750791666666, K0 = 1, FE = 1000000, FN = 1000000 },
            new ProjectedCrs { Name = "MAGNA-SIRGAS / Colombia Oeste Oeste",  Epsg = 3114, A = GrsA, F = GrsF, Lat0 = MagnaLat0, Lon0 = -80.07750791666666, K0 = 1, FE = 1000000, FN = 1000000 },
            new ProjectedCrs { Name = "MAGNA-SIRGAS / Colombia Este Central", Epsg = 3117, A = GrsA, F = GrsF, Lat0 = MagnaLat0, Lon0 = -71.07750791666666, K0 = 1, FE = 1000000, FN = 1000000 },
            new ProjectedCrs { Name = "MAGNA-SIRGAS / Colombia Este Este",    Epsg = 3118, A = GrsA, F = GrsF, Lat0 = MagnaLat0, Lon0 = -68.07750791666666, K0 = 1, FE = 1000000, FN = 1000000 },
            new ProjectedCrs { Name = "WGS 84 / UTM zona 17N",               Epsg = 32617, A = WgsA, F = WgsF, Lat0 = 0, Lon0 = -81, K0 = 0.9996, FE = 500000, FN = 0 },
            new ProjectedCrs { Name = "WGS 84 / UTM zona 18N",               Epsg = 32618, A = WgsA, F = WgsF, Lat0 = 0, Lon0 = -75, K0 = 0.9996, FE = 500000, FN = 0 },
            new ProjectedCrs { Name = "WGS 84 / UTM zona 19N",               Epsg = 32619, A = WgsA, F = WgsF, Lat0 = 0, Lon0 = -69, K0 = 0.9996, FE = 500000, FN = 0 },
        };
    }

    /// <summary>
    /// Transverse Mercator con series de Krüger hasta n^6 (precisión submilimétrica
    /// en todo el ancho de Colombia, incluso en Origen Nacional con ±7° del meridiano central).
    /// </summary>
    public static class Projection
    {
        private const double Deg = Math.PI / 180.0;

        public static void Forward(ProjectedCrs crs, double latDeg, double lonDeg, out double north, out double east)
        {
            double n = crs.F / (2 - crs.F);
            double n2 = n * n, n3 = n2 * n, n4 = n3 * n, n5 = n4 * n, n6 = n5 * n;
            double A = crs.A / (1 + n) * (1 + n2 / 4 + n4 / 64 + n6 / 256);

            double[] alpha =
            {
                n / 2 - 2.0 / 3 * n2 + 5.0 / 16 * n3 + 41.0 / 180 * n4 - 127.0 / 288 * n5 + 7891.0 / 37800 * n6,
                13.0 / 48 * n2 - 3.0 / 5 * n3 + 557.0 / 1440 * n4 + 281.0 / 630 * n5 - 1983433.0 / 1935360 * n6,
                61.0 / 240 * n3 - 103.0 / 140 * n4 + 15061.0 / 26880 * n5 + 167603.0 / 181440 * n6,
                49561.0 / 161280 * n4 - 179.0 / 168 * n5 + 6601661.0 / 7257600 * n6,
                34729.0 / 80640 * n5 - 3418889.0 / 1995840 * n6,
                212378941.0 / 319334400 * n6
            };

            double e = 2 * Math.Sqrt(n) / (1 + n);

            // Punto
            Gauss(latDeg * Deg, (lonDeg - crs.Lon0) * Deg, e, alpha, out double xi, out double eta);
            // Origen (en el meridiano central)
            Gauss(crs.Lat0 * Deg, 0, e, alpha, out double xi0, out _);

            east = crs.FE + crs.K0 * A * eta;
            north = crs.FN + crs.K0 * A * (xi - xi0);
        }

        private static void Gauss(double phi, double lam, double e, double[] alpha, out double xi, out double eta)
        {
            double s = Math.Sin(phi);
            double t = Math.Sinh(Atanh(s) - e * Atanh(e * s));   // tangente de la latitud conforme
            double xiP = Math.Atan2(t, Math.Cos(lam));
            double etaP = Atanh(Math.Sin(lam) / Math.Sqrt(1 + t * t));

            xi = xiP;
            eta = etaP;
            for (int j = 1; j <= alpha.Length; j++)
            {
                xi += alpha[j - 1] * Math.Sin(2 * j * xiP) * Math.Cosh(2 * j * etaP);
                eta += alpha[j - 1] * Math.Cos(2 * j * xiP) * Math.Sinh(2 * j * etaP);
            }
        }

        // .NET Framework 4.8 no tiene Math.Atanh
        private static double Atanh(double x) => 0.5 * Math.Log((1 + x) / (1 - x));

        // ---------------------------------------------------------------------
        // Lectura de ángulos: decimal (4.6034 / 4,6034 / -74.0751) o GMS
        // (4°36'12.5"N, 74 04 30.2 W, -74:04:30.2, 74°4'30,2"O)
        // ---------------------------------------------------------------------
        public static bool TryParseAngle(string s, out double deg)
        {
            deg = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().ToUpperInvariant();

            int sign = 1;
            // Hemisferio: S, W y O (Oeste) son negativos
            var hemi = Regex.Match(s, @"^[NSEWO]|[NSEWO]$");
            if (hemi.Success)
            {
                char h = hemi.Value[0];
                if (h == 'S' || h == 'W' || h == 'O') sign = -1;
                s = s.Remove(hemi.Index, 1).Trim();
            }
            if (s.StartsWith("-")) { sign = -sign; s = s.Substring(1).Trim(); }
            else if (s.StartsWith("+")) s = s.Substring(1).Trim();

            // Símbolos de grados/minutos/segundos -> espacio
            s = Regex.Replace(s, "[°º˚'’′\"”″:]", " ").Trim();
            string[] parts = Regex.Split(s, @"\s+");

            if (parts.Length == 1)
            {
                if (!CsvPointReader.TryParseNumber(parts[0], out deg)) return false;
            }
            else if (parts.Length <= 3)
            {
                double d = 0, m = 0, sec = 0;
                if (!CsvPointReader.TryParseNumber(parts[0], out d)) return false;
                if (!CsvPointReader.TryParseNumber(parts[1], out m) || m < 0 || m >= 60) return false;
                if (parts.Length == 3 && (!CsvPointReader.TryParseNumber(parts[2], out sec) || sec < 0 || sec >= 60)) return false;
                deg = d + m / 60.0 + sec / 3600.0;
            }
            else return false;

            deg *= sign;
            return true;
        }
    }
}
