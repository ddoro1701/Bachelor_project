import React, { useEffect, useState } from 'react';

export default function CenterNotice() {
  const [notice, setNotice] = useState(null);

  useEffect(() => {
    const onNotice = (e) => {
      const { type = 'info', message = '', lecturer } = e.detail || {};
      setNotice({ type, message, lecturer });
    };
    window.addEventListener('notice', onNotice);
    return () => window.removeEventListener('notice', onNotice);
  }, []);

  if (!notice) return null;

  const close = () => setNotice(null);

  return (
    <div className="notice-overlay" onClick={close} role="dialog" aria-modal="true">
      <div className={`notice-card ${notice.type}`} onClick={(e) => e.stopPropagation()}>
        <div className="notice-title">
          {notice.type === 'error' ? 'Error' : notice.type === 'success' ? 'Success' : 'Notice'}
        </div>
        <div className="notice-msg">
          {notice.message}{' '}
          {notice.lecturer ? <strong className="notice-lecturer">{notice.lecturer}</strong> : null}
        </div>
        <button className="notice-close" onClick={close}>OK</button>
      </div>
    </div>
  );
}