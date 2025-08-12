# TCN Spike Pipeline

This project implements an end-to-end pipeline for training a Temporal Convolutional Network (TCN) to predict spike probabilities from OHLCV market data using Heikin-Ashi-based features and technical indicators.

## Pipeline
- Raw OHLCV → Heikin-Ashi (HA) transform
- Parallel feature classes:
  - HA External (metrics on HA price): slope, angle, acceleration
  - HA Internal (metrics on original OHLC): slope, angle, acceleration
  - Indicators (ROC, volatility, PSI, momentum, rolling z-score)
- Feature aggregation
- Normalization (Z-score fit on train only)
- Sequence windowing (N consecutive candles)
- Label generation (Entry=1, Exit=1, rest=0 based on forward returns)
- TCN model (causal dilated conv) outputs spike probabilities for the last step in each window

## Quickstart
```bash
pip install -r requirements.txt
python -m tcn_spike.demo
```

The demo generates synthetic OHLCV data to validate the pipeline.