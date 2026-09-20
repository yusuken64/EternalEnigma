# Generation

Home for seeded generation, stable stage streams and generated world descriptions.
Accept seeds and configuration explicitly; do not read clocks, global random state,
Unity assets or scene objects. `CampaignGenerator.Generate(seed)` returns a validated
logical world. `CampaignFingerprint` supplies canonical content and a SHA-256 digest.
