using UnityEngine;

public static class TenantReviewCandidate
{
    public struct Candidate
    {
        public string Id;
        public string DisplayName;
        public Color Color;
    }

    public static readonly Candidate[] All = new Candidate[]
    {
        new Candidate { Id = "tenant_alpha",   DisplayName = "Alpha",   Color = new Color(0.90f, 0.30f, 0.30f, 1f) },
        new Candidate { Id = "tenant_beta",    DisplayName = "Beta",    Color = new Color(0.30f, 0.80f, 0.30f, 1f) },
        new Candidate { Id = "tenant_gamma",   DisplayName = "Gamma",   Color = new Color(0.30f, 0.40f, 0.90f, 1f) },
        new Candidate { Id = "tenant_delta",   DisplayName = "Delta",   Color = new Color(0.95f, 0.75f, 0.20f, 1f) },
        new Candidate { Id = "tenant_epsilon", DisplayName = "Epsilon", Color = new Color(0.80f, 0.30f, 0.80f, 1f) },
        new Candidate { Id = "tenant_zeta",    DisplayName = "Zeta",    Color = new Color(0.30f, 0.85f, 0.85f, 1f) },
        new Candidate { Id = "tenant_eta",     DisplayName = "Eta",     Color = new Color(0.95f, 0.55f, 0.25f, 1f) },
        new Candidate { Id = "tenant_theta",   DisplayName = "Theta",   Color = new Color(0.60f, 0.40f, 0.20f, 1f) },
        new Candidate { Id = "tenant_iota",    DisplayName = "Iota",    Color = new Color(0.75f, 0.75f, 0.75f, 1f) },
        new Candidate { Id = "tenant_kappa",   DisplayName = "Kappa",   Color = new Color(0.90f, 0.60f, 0.60f, 1f) },
        new Candidate { Id = "tenant_lambda",  DisplayName = "Lambda",  Color = new Color(0.40f, 0.70f, 0.40f, 1f) },
        new Candidate { Id = "tenant_mu",      DisplayName = "Mu",      Color = new Color(0.50f, 0.50f, 0.90f, 1f) },
        new Candidate { Id = "tenant_nu",      DisplayName = "Nu",      Color = new Color(0.90f, 0.85f, 0.50f, 1f) },
        new Candidate { Id = "tenant_xi",      DisplayName = "Xi",      Color = new Color(0.60f, 0.30f, 0.60f, 1f) },
        new Candidate { Id = "tenant_omicron", DisplayName = "Omicron", Color = new Color(0.20f, 0.70f, 0.70f, 1f) },
        new Candidate { Id = "tenant_pi",      DisplayName = "Pi",      Color = new Color(0.85f, 0.45f, 0.15f, 1f) },
        new Candidate { Id = "tenant_rho",     DisplayName = "Rho",     Color = new Color(0.45f, 0.30f, 0.15f, 1f) },
        new Candidate { Id = "tenant_sigma",   DisplayName = "Sigma",   Color = new Color(0.55f, 0.55f, 0.55f, 1f) },
        new Candidate { Id = "tenant_tau",     DisplayName = "Tau",     Color = new Color(0.70f, 0.30f, 0.50f, 1f) },
        new Candidate { Id = "tenant_upsilon", DisplayName = "Upsilon", Color = new Color(0.30f, 0.60f, 0.50f, 1f) },
    };

    public static Candidate[] GetShuffledOrder(int seed)
    {
        var result = new Candidate[All.Length];
        System.Array.Copy(All, result, All.Length);

        var rng = new System.Random(seed);
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = result[i];
            result[i] = result[j];
            result[j] = temp;
        }

        return result;
    }

    public static bool TryFindById(string candidateId, out Candidate candidate)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Id == candidateId)
            {
                candidate = All[i];
                return true;
            }
        }
        candidate = default;
        return false;
    }
}
