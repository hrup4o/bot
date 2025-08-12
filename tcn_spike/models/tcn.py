from typing import List, Tuple
import torch
import torch.nn as nn
import torch.nn.functional as F


class Chomp1d(nn.Module):
    def __init__(self, chomp_size: int) -> None:
        super().__init__()
        self.chomp_size = chomp_size

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        if self.chomp_size == 0:
            return x
        return x[:, :, :-self.chomp_size]


class TemporalBlock(nn.Module):
    def __init__(
        self,
        in_channels: int,
        out_channels: int,
        kernel_size: int,
        stride: int,
        dilation: int,
        padding: int,
        dropout: float = 0.0,
    ) -> None:
        super().__init__()
        self.conv1 = nn.Conv1d(
            in_channels,
            out_channels,
            kernel_size,
            stride=stride,
            padding=padding,
            dilation=dilation,
        )
        self.chomp1 = Chomp1d(padding)
        self.relu1 = nn.ReLU()
        self.dropout1 = nn.Dropout(dropout)

        self.conv2 = nn.Conv1d(
            out_channels,
            out_channels,
            kernel_size,
            stride=stride,
            padding=padding,
            dilation=dilation,
        )
        self.chomp2 = Chomp1d(padding)
        self.relu2 = nn.ReLU()
        self.dropout2 = nn.Dropout(dropout)

        self.downsample = (
            nn.Conv1d(in_channels, out_channels, kernel_size=1)
            if in_channels != out_channels
            else None
        )
        self.final_relu = nn.ReLU()

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        out = self.conv1(x)
        out = self.chomp1(out)
        out = self.relu1(out)
        out = self.dropout1(out)

        out = self.conv2(out)
        out = self.chomp2(out)
        out = self.relu2(out)
        out = self.dropout2(out)

        res = x if self.downsample is None else self.downsample(x)
        return self.final_relu(out + res)


class TCNBackbone(nn.Module):
    def __init__(
        self,
        num_features: int,
        num_channels: List[int],
        kernel_size: int = 3,
        dropout: float = 0.0,
    ) -> None:
        super().__init__()
        layers: List[nn.Module] = []
        num_levels = len(num_channels)
        in_ch = num_features
        for i in range(num_levels):
            out_ch = num_channels[i]
            dilation_size = 2 ** i
            padding = (kernel_size - 1) * dilation_size
            layers += [
                TemporalBlock(
                    in_channels=in_ch,
                    out_channels=out_ch,
                    kernel_size=kernel_size,
                    stride=1,
                    dilation=dilation_size,
                    padding=padding,
                    dropout=dropout,
                )
            ]
            in_ch = out_ch
        self.network = nn.Sequential(*layers)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.network(x)


class TCNModel(nn.Module):
    def __init__(
        self,
        num_features: int,
        num_targets: int,
        hidden_channels: List[int] = [32, 64, 64],
        kernel_size: int = 3,
        dropout: float = 0.1,
    ) -> None:
        super().__init__()
        self.backbone = TCNBackbone(
            num_features=num_features,
            num_channels=hidden_channels,
            kernel_size=kernel_size,
            dropout=dropout,
        )
        self.head = nn.Linear(hidden_channels[-1], num_targets)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        feats = self.backbone(x)
        last = feats[:, :, -1]
        logits = self.head(last)
        return logits