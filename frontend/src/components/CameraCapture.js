import React, { useEffect, useRef, useState } from 'react';
import './CameraCapture.css';

export default function CameraCapture({ open, onClose, onCapture }) {
  const videoRef = useRef(null);
  const streamRef = useRef(null);
  const [track, setTrack] = useState(null);
  const [error, setError] = useState('');

  // Höchste sinnvolle Defaults anfordern (Browser verhandelt runter wenn nötig)
  const defaultConstraints = {
    video: {
      facingMode: { ideal: 'environment' },
      width:  { min: 1280, ideal: 3840, max: 4096 }, // bis ~4K
      height: { min: 720,  ideal: 2160, max: 4096 },
      frameRate: { ideal: 30, max: 60 },
      advanced: [{ focusMode: 'continuous' }] // falls unterstützt
    },
    audio: false
  };

  async function start(constraints) {
    stop();
    try {
      const stream = await navigator.mediaDevices.getUserMedia(constraints);
      streamRef.current = stream;
      const [vt] = stream.getVideoTracks();
      setTrack(vt);
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
      }
      // Versuche nach dem Start die höchste Track-Auflösung zu bekommen
      try {
        const caps = vt.getCapabilities?.();
        const idealW = Math.min(4096, caps?.width?.max || 4096);
        const idealH = Math.min(4096, caps?.height?.max || 4096);
        await vt.applyConstraints({
          width: { ideal: idealW },
          height: { ideal: idealH },
          advanced: [
            ...(caps?.focusMode?.includes?.('continuous') ? [{ focusMode: 'continuous' }] : []),
            ...(caps?.exposureMode?.includes?.('continuous') ? [{ exposureMode: 'continuous' }] : []),
            ...(caps?.whiteBalanceMode?.includes?.('continuous') ? [{ whiteBalanceMode: 'continuous' }] : []),
          ]
        });
      } catch {}
      setError('');
    } catch (e) {
      console.error(e);
      setError('Camera access denied or unavailable.');
    }
  }

  function stop() {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(t => t.stop());
      streamRef.current = null;
    }
    setTrack(null);
  }

  useEffect(() => {
    if (open) start(defaultConstraints);
    return () => stop();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const capture = async () => {
    try {
      if (track && 'ImageCapture' in window) {
        const ic = new window.ImageCapture(track);
        const blob = await ic.takePhoto().catch(() => null); // liefert oft Vollauflösung
        if (blob) { await onCapture(blob); onClose(); return; }
      }
    } catch (e) { console.warn('ImageCapture failed, falling back to canvas:', e); }

    // Fallback: Frame in nativer Videoauflösung
    if (!videoRef.current) return;
    const v = videoRef.current;
    const w = v.videoWidth || 1920;
    const h = v.videoHeight || 1080;
    const canvas = document.createElement('canvas');
    canvas.width = w; canvas.height = h;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(v, 0, 0, w, h);
    canvas.toBlob(async (blob) => {
      if (blob) { await onCapture(blob); onClose(); }
    }, 'image/jpeg', 0.95);
  };

  if (!open) return null;

  return (
    <div className="camera-overlay" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="camera-sheet" onClick={(e) => e.stopPropagation()}>
        <div className="camera-frame">
          <video ref={videoRef} playsInline muted />
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