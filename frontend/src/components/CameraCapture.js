import React, { useEffect, useRef, useState } from 'react';
import './CameraCapture.css';

export default function CameraCapture({ open, onClose, onCapture }) {
  const videoRef = useRef(null);
  const streamRef = useRef(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!open) {
      if (streamRef.current) {
        streamRef.current.getTracks().forEach(t => t.stop());
        streamRef.current = null;
      }
      return;
    }
    (async () => {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: 'environment' } },
          audio: false
        });
        streamRef.current = stream;
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          await videoRef.current.play();
        }
      } catch (e) {
        setError('Camera access denied or unavailable.');
      }
    })();
    return () => {
      if (streamRef.current) {
        streamRef.current.getTracks().forEach(t => t.stop());
        streamRef.current = null;
      }
    };
  }, [open]);

  const capture = async () => {
    if (!videoRef.current) return;
    const video = videoRef.current;
    // Capture at reasonable size
    const w = Math.min(1280, video.videoWidth || 1280);
    const h = Math.round((w / (video.videoWidth || w)) * (video.videoHeight || 720));
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(video, 0, 0, w, h);
    canvas.toBlob(async (blob) => {
      if (blob) {
        await onCapture(blob);
        onClose();
      }
    }, 'image/jpeg', 0.9);
  };

  if (!open) return null;

  return (
    <div className="camera-overlay" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="camera-sheet" onClick={(e) => e.stopPropagation()}>
        <div className="camera-frame">
          <video ref={videoRef} playsInline muted />
          {/* Overlay guides */}
          <div className="guides">
            <div className="grid v1" />
            <div className="grid v2" />
            <div className="grid h1" />
            <div className="grid h2" />
            <div className="corners tl" />
            <div className="corners tr" />
            <div className="corners bl" />
            <div className="corners br" />
          </div>
        </div>
        {error && <div className="camera-error">{error}</div>}
        <div className="camera-controls">
          <button onClick={onClose}>Close</button>
          <button onClick={capture}>Capture</button>
        </div>
      </div>
    </div>
  );
}