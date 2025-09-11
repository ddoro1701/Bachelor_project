import React, { useEffect, useState } from 'react';

export default function CenterNotice() {
  const [notice, setNotice] = useState(null);
  const [confirm, setConfirm] = useState(null);

  useEffect(() => {
    const onNotice = (e) => setNotice({ type: e.detail?.type || 'info', message: e.detail?.message || '' });
    const onConfirm = (e) => {
      const d = e.detail || {};
      setConfirm({
        message: d.message || 'Are you sure?',
        confirmText: d.confirmText || 'Confirm',
        cancelText: d.cancelText || 'Cancel',
        onConfirm: typeof d.onConfirm === 'function' ? d.onConfirm : null,
        onCancel: typeof d.onCancel === 'function' ? d.onCancel : null
      });
    };
    window.addEventListener('notice', onNotice);
    window.addEventListener('confirm', onConfirm);
    return () => {
      window.removeEventListener('notice', onNotice);
      window.removeEventListener('confirm', onConfirm);
    };
  }, []);

  const closeNotice = () => setNotice(null);

  return (
    <>
      {notice && (
        <div className="cn-overlay" onClick={closeNotice}>
          <div className="cn-modal" onClick={(e) => e.stopPropagation()}>
            <div className={`cn-title ${notice.type}`}>{notice.type === 'warning' ? 'Warning' : 'Info'}</div>
            <div className="cn-body">{notice.message}</div>
            <div className="cn-actions">
              <button className="btn" onClick={closeNotice}>OK</button>
            </div>
          </div>
        </div>
      )}

      {confirm && (
        <div className="cn-overlay" onClick={() => setConfirm(null)}>
          <div className="cn-modal" onClick={(e) => e.stopPropagation()}>
            <div className="cn-title warning">Please confirm</div>
            <div className="cn-body">{confirm.message}</div>
            <div className="cn-actions">
              <button className="btn ghost" onClick={() => { confirm.onCancel?.(); setConfirm(null); }}>
                {confirm.cancelText}
              </button>
              <button
                className="btn danger"
                onClick={() => { confirm.onConfirm?.(); setConfirm(null); }}
              >
                {confirm.confirmText}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}