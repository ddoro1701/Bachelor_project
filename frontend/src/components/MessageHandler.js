import React, { useEffect, useState } from 'react';

let seq = 0;
export default function MessageHandler() {
  const [toasts, setToasts] = useState([]);
  useEffect(() => {
    const onToast = (e) => {
      const { type = 'info', message = '' } = e.detail || {};
      const id = ++seq;
      setToasts(t => [...t, { id, type, message }]);
      setTimeout(() => setToasts(t => t.filter(x => x.id !== id)), 3500);
    };
    window.addEventListener('toast', onToast);
    return () => window.removeEventListener('toast', onToast);
  }, []);
  return (
    <div className="toast-container">
      {toasts.map(t => (<div key={t.id} className={`toast ${t.type}`}>{t.message}</div>))}
    </div>
  );
}