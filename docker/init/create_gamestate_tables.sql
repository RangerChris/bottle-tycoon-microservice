-- Create required tables for GameService if they don't exist
CREATE TABLE IF NOT EXISTS public.players (
    Id uuid PRIMARY KEY,
    Credits numeric(18,2) NOT NULL,
    CreatedAt timestamp with time zone NOT NULL,
    UpdatedAt timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS public.purchases (
    Id uuid PRIMARY KEY,
    PlayerId uuid NOT NULL,
    ItemType text NOT NULL,
    Amount numeric(18,2) NOT NULL,
    PurchasedAt timestamp with time zone NOT NULL,
    CONSTRAINT fk_purchases_players_playerid FOREIGN KEY (PlayerId) REFERENCES public.players(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Purchases_PlayerId ON public.purchases (PlayerId);

CREATE TABLE IF NOT EXISTS public.upgrades (
    Id uuid PRIMARY KEY,
    PlayerId uuid NOT NULL,
    ItemType text NOT NULL,
    ItemId integer NOT NULL,
    NewLevel integer NOT NULL,
    Cost numeric(18,2) NOT NULL,
    UpgradedAt timestamp with time zone NOT NULL,
    CONSTRAINT fk_upgrades_players_playerid FOREIGN KEY (PlayerId) REFERENCES public.players(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Upgrades_PlayerId ON public.upgrades (PlayerId);