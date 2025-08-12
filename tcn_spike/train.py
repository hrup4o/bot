from dataclasses import dataclass
from typing import List, Optional, Tuple

import numpy as np
import pandas as pd
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset

from .data.ha import compute_heikin_ashi
from .features.ha_external import compute_ha_external_metrics
from .features.ha_internal import compute_ha_internal_metrics
from .features.indicators import compute_indicator_metrics
from .features.aggregator import FeatureAggregator
from .preprocessing.normalization import StandardScalerTimeSeries
from .preprocessing.window import SequenceWindowGenerator
from .labels.labeler import generate_entry_exit_labels
from .models.tcn import TCNModel


@dataclass
class TrainConfig:
    ha_windows: List[int]
    ind_windows: List[int]
    seq_len: int
    batch_size: int = 64
    epochs: int = 5
    lr: float = 1e-3
    dropout: float = 0.1
    hidden_channels: List[int] = (32, 64, 64)
    entry_threshold: float = 0.01
    exit_threshold: float = 0.01
    label_horizon: int = 5
    train_frac: float = 0.7
    device: str = "cpu"


def build_features_and_labels(
    ohlcv: pd.DataFrame, cfg: TrainConfig
) -> Tuple[pd.DataFrame, pd.DataFrame]:
    ha_df = compute_heikin_ashi(ohlcv)
    ha_ext = compute_ha_external_metrics(ha_df, cfg.ha_windows)
    ha_int = compute_ha_internal_metrics(ohlcv, cfg.ha_windows)
    inds = compute_indicator_metrics(ohlcv, cfg.ind_windows)

    aggregator = FeatureAggregator(include_ha_ohlc=True)
    features = aggregator.aggregate([ha_ext, ha_int, inds, ha_df])

    labels = generate_entry_exit_labels(
        ohlcv["close"],
        horizon=cfg.label_horizon,
        entry_threshold=cfg.entry_threshold,
        exit_threshold=cfg.exit_threshold,
    )

    df = pd.concat([features, labels], axis=1)
    df = df.dropna()
    features = df[features.columns]
    labels = df[labels.columns]

    return features, labels


def train_model(ohlcv: pd.DataFrame, cfg: TrainConfig) -> Tuple[TCNModel, StandardScalerTimeSeries]:
    features, labels = build_features_and_labels(ohlcv, cfg)

    n = len(features)
    train_n = int(n * cfg.train_frac)
    train_slice = slice(0, train_n)

    scaler = StandardScalerTimeSeries()
    features_scaled = scaler.fit_transform(features, train_slice=train_slice)

    X_all = features_scaled.values.astype(np.float32)
    y_all = labels.values.astype(np.float32)

    windower = SequenceWindowGenerator(seq_len=cfg.seq_len, stride=1)
    X, y = windower.generate(X_all, y_all)

    num_features = X.shape[1]
    num_targets = y.shape[1]

    X_train, y_train = X[: train_n - cfg.seq_len + 1], y[: train_n - cfg.seq_len + 1]
    X_valid, y_valid = X[train_n - cfg.seq_len + 1 :], y[train_n - cfg.seq_len + 1 :]

    train_ds = TensorDataset(
        torch.from_numpy(X_train), torch.from_numpy(y_train)
    )
    valid_ds = TensorDataset(
        torch.from_numpy(X_valid), torch.from_numpy(y_valid)
    )

    train_loader = DataLoader(train_ds, batch_size=cfg.batch_size, shuffle=True)
    valid_loader = DataLoader(valid_ds, batch_size=cfg.batch_size, shuffle=False)

    model = TCNModel(
        num_features=num_features,
        num_targets=num_targets,
        hidden_channels=list(cfg.hidden_channels),
        dropout=cfg.dropout,
    ).to(cfg.device)

    optimizer = torch.optim.Adam(model.parameters(), lr=cfg.lr)
    criterion = nn.BCEWithLogitsLoss()

    for epoch in range(cfg.epochs):
        model.train()
        total_loss = 0.0
        for xb, yb in train_loader:
            xb = xb.to(cfg.device)
            yb = yb.to(cfg.device)
            optimizer.zero_grad()
            logits = model(xb)
            loss = criterion(logits, yb)
            loss.backward()
            optimizer.step()
            total_loss += loss.item() * xb.size(0)
        train_loss = total_loss / len(train_loader.dataset)

        model.eval()
        with torch.no_grad():
            total_v = 0.0
            for xb, yb in valid_loader:
                xb = xb.to(cfg.device)
                yb = yb.to(cfg.device)
                logits = model(xb)
                loss = criterion(logits, yb)
                total_v += loss.item() * xb.size(0)
            valid_loss = total_v / max(1, len(valid_loader.dataset))
        print(f"Epoch {epoch+1}/{cfg.epochs} - train_loss={train_loss:.4f} valid_loss={valid_loss:.4f}")

    return model, scaler