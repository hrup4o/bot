import numpy as np
from typing import Optional, Tuple


class SequenceWindowGenerator:
    """Generates sliding windows for sequences.

    Produces arrays suitable for TCN: X shape (num_samples, num_features, seq_len).
    If labels are provided as array shape (num_timesteps, num_targets), y will be
    shape (num_samples, num_targets) for the last step in each window.
    """

    def __init__(self, seq_len: int, stride: int = 1) -> None:
        self.seq_len = seq_len
        self.stride = stride

    def generate(
        self,
        features: np.ndarray,
        labels: Optional[np.ndarray] = None,
    ) -> Tuple[np.ndarray, Optional[np.ndarray]]:
        num_timesteps, num_features = features.shape
        indices = []
        start = 0
        while start + self.seq_len <= num_timesteps:
            indices.append((start, start + self.seq_len))
            start += self.stride

        num_samples = len(indices)
        X = np.zeros((num_samples, num_features, self.seq_len), dtype=np.float32)
        y = None
        if labels is not None:
            if labels.ndim == 1:
                labels = labels[:, None]
            num_targets = labels.shape[1]
            y = np.zeros((num_samples, num_targets), dtype=np.float32)

        for i, (s, e) in enumerate(indices):
            X[i] = features[s:e].T
            if labels is not None:
                y[i] = labels[e - 1]

        return X, y